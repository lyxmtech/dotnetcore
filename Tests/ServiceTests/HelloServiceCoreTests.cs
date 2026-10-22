using Contracts;
using DataCore.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceCore.Hello;
using Xunit;

namespace ServiceTests;

public class HelloServiceCoreTests
{
    private static HelloServiceCore CreateCore(RecordingEventBus bus, RequestsRepository repository) =>
        new(bus, repository, NullLogger<HelloServiceCore>.Instance);

    [Fact]
    public async Task HandleHelloAsync_StoresRequest_AndPublishesEvent()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        var bus = new RecordingEventBus();
        var core = CreateCore(bus, repository);

        var at = new DateTimeOffset(2026, 10, 21, 12, 30, 0, TimeSpan.Zero);
        var result = await core.HandleHelloAsync("Alice", at);

        Assert.True(result.RequestId > 0);
        Assert.Equal("Alice", result.Name);

        // The durable side effect landed in the requests table.
        var row = Assert.Single(await repository.GetRecentAsync());
        Assert.Equal(result.RequestId, row.Id);
        Assert.Equal("Alice", row.Name);
        Assert.Equal(at, row.RequestedAt);

        // ...and the event was published with the same data.
        var evt = bus.Single<HelloRequested>();
        Assert.Equal("Alice", evt.Name);
        Assert.Equal(at, evt.At);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleHelloAsync_BlankName_FallsBackToWorld(string? name)
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        var bus = new RecordingEventBus();
        var core = CreateCore(bus, repository);

        var result = await core.HandleHelloAsync(name, DateTimeOffset.UtcNow);

        Assert.Equal(HelloServiceCore.DefaultName, result.Name);
        Assert.Equal("World", result.Name);

        var row = Assert.Single(await repository.GetRecentAsync());
        Assert.Equal("World", row.Name);

        var evt = bus.Single<HelloRequested>();
        Assert.Equal("World", evt.Name);
    }

    [Fact]
    public async Task HandleHelloAsync_MultipleCalls_StoreOneRowEach_AndPublishEachEvent()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        var bus = new RecordingEventBus();
        var core = CreateCore(bus, repository);

        var now = DateTimeOffset.UtcNow;
        await core.HandleHelloAsync("first", now);
        await core.HandleHelloAsync("second", now.AddMinutes(1));

        Assert.Equal(2, (await repository.GetRecentAsync()).Count);
        Assert.Equal(2, bus.All<HelloRequested>().Count);
    }

    [Fact]
    public async Task HandleHelloAsync_NewestRequestComesFirst()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        var core = CreateCore(new RecordingEventBus(), repository);

        var now = DateTimeOffset.UtcNow;
        await core.HandleHelloAsync("first", now);
        await core.HandleHelloAsync("second", now.AddMinutes(1));

        var rows = await repository.GetRecentAsync();
        Assert.Equal("second", rows[0].Name);
        Assert.Equal("first", rows[1].Name);
    }

    [Fact]
    public async Task HandleHelloAsync_EventIsPublishedAfterTheRowIsStored()
    {
        // The durable write must happen before the publish, otherwise a
        // consumer could react to an event that was never persisted.
        using var db = new TempDatabase();
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        var bus = new RecordingEventBus();
        var core = CreateCore(bus, repository);

        await core.HandleHelloAsync("Alice", DateTimeOffset.UtcNow);

        Assert.Single(await repository.GetRecentAsync());
        Assert.Equal(1, bus.Count);
    }

    [Fact]
    public void ResolveName_NormalisesBlankNames()
    {
        Assert.Equal("World", HelloServiceCore.ResolveName(null));
        Assert.Equal("World", HelloServiceCore.ResolveName(""));
        Assert.Equal("World", HelloServiceCore.ResolveName("  "));
        Assert.Equal("Alice", HelloServiceCore.ResolveName("Alice"));
    }
}
