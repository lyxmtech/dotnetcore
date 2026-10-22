using Contracts;
using DataCore.Audit;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceCore.Audit;
using Xunit;

namespace ServiceTests;

public class AuditConsumerTests
{
    [Fact]
    public async Task Consume_PersistsTheEventToTheAuditDatabase()
    {
        await using var harness = await AuditHarness.StartAsync();
        var requestedAt = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

        await harness.Bus.Publish(new HelloRequested("Alice", requestedAt));

        await harness.WaitForConsumedAsync(1);
        var row = Assert.Single(await harness.Repository.GetRecentAsync());
        Assert.Equal("Alice", row.Name);
        Assert.Equal(requestedAt, row.RequestedAt);
    }

    [Fact]
    public async Task Consume_BumpsTheAuditCounter()
    {
        await using var harness = await AuditHarness.StartAsync();

        await harness.Bus.Publish(new HelloRequested("Alice", DateTimeOffset.UtcNow));
        await harness.WaitForConsumedAsync(1);
        var stats = await harness.Repository.GetStatsAsync();
        Assert.Equal(1, stats.Total);
    }

    [Fact]
    public async Task Consume_MultipleEvents_AreAllPersisted()
    {
        await using var harness = await AuditHarness.StartAsync();

        await harness.Bus.Publish(new HelloRequested("first", DateTimeOffset.UtcNow));
        await harness.Bus.Publish(new HelloRequested("second", DateTimeOffset.UtcNow));

        await harness.WaitForConsumedAsync(2);
        var rows = await harness.Repository.GetRecentAsync();
        Assert.Equal(2, rows.Count);

        var stats = await harness.Repository.GetStatsAsync();
        Assert.Equal(2, stats.Total);
    }

    [Fact]
    public async Task Consume_DoesNotTouchTheRequestsTable()
    {
        // "Database per service": the audit service owns the audit tables only.
        await using var harness = await AuditHarness.StartAsync();

        await harness.Bus.Publish(new HelloRequested("Alice", DateTimeOffset.UtcNow));
        await harness.WaitForConsumedAsync(1);
        await using var connection = await harness.Database.CreateFactory().CreateAsync();
        var tables = await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';");

        Assert.Contains("audit", tables);
        Assert.DoesNotContain("requests", tables);
    }
}

public class AuditServiceCoreTests
{
    private static AuditServiceCore CreateCore(TempDatabase db) =>
        new(
            new AuditRepository(db.CreateFactory(), NullLogger<AuditRepository>.Instance),
            NullLogger<AuditServiceCore>.Instance);

    [Fact]
    public async Task RecordAsync_StoresTheRowAndBumpsTheCounter()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(AuditSchema.Statements);
        var core = CreateCore(db);

        var requestedAt = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
        var recordedAt = new DateTimeOffset(2026, 10, 21, 9, 0, 5, TimeSpan.Zero);

        var id = await core.RecordAsync("Alice", requestedAt, recordedAt);

        Assert.True(id > 0);

        var row = Assert.Single(await new AuditRepository(db.CreateFactory(), NullLogger<AuditRepository>.Instance).GetRecentAsync());
        Assert.Equal("Alice", row.Name);
        Assert.Equal(requestedAt, row.RequestedAt);
        Assert.Equal(recordedAt, row.RecordedAt);

        var stats = await new AuditRepository(db.CreateFactory(), NullLogger<AuditRepository>.Instance).GetStatsAsync();
        Assert.Equal(1, stats.Total);
    }

    [Fact]
    public async Task RecordAsync_WithoutRecordedAt_UsesTheCurrentTime()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(AuditSchema.Statements);
        var core = CreateCore(db);

        var requestedAt = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
        var before = DateTimeOffset.UtcNow;

        await core.RecordAsync("Alice", requestedAt);

        var row = Assert.Single(await new AuditRepository(db.CreateFactory(), NullLogger<AuditRepository>.Instance).GetRecentAsync());
        Assert.InRange(row.RecordedAt, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }
}


