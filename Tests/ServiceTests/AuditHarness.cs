using Contracts;
using DataCore.Audit;
using DbCore;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ServiceTests;

/// <summary>
/// Builds the AuditService's own DI graph (repository + core + consumer) on top
/// of a throw-away database, plus an in-memory MassTransit bus so a published
/// event really travels to the consumer - without needing a RabbitMQ broker.
/// </summary>
public sealed class AuditHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private AuditHarness(ServiceProvider provider, TempDatabase database)
    {
        _provider = provider;
        Database = database;
    }

    public TempDatabase Database { get; }

    public AuditRepository Repository => _provider.GetRequiredService<AuditRepository>();

    public IBus Bus => _provider.GetRequiredService<IBus>();

    public static async Task<AuditHarness> StartAsync()
    {
        var database = new TempDatabase();
        await database.InitializeAsync(AuditSchema.Statements);

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IDbConnectionFactory>(database.CreateFactory());
        services.AddSingleton<AuditRepository>();
        services.AddSingleton<ServiceCore.Audit.AuditServiceCore>();
        services.AddSingleton<ServiceCore.Audit.AuditConsumer>();

        // The in-memory transport replaces RabbitMQ: the consumer still receives
        // the event, but nothing has to be listening on a broker.
        services.AddMassTransitTestHarness(x => x.AddConsumer<ServiceCore.Audit.AuditConsumer>());

        var provider = services.BuildServiceProvider();
        var harness = new AuditHarness(provider, database);

        var testHarness = provider.GetRequiredService<ITestHarness>();
        await testHarness.Start();

        return harness;
    }

    /// <summary>The harness of the audit consumer, used to await the message.</summary>
    public IConsumerTestHarness<ServiceCore.Audit.AuditConsumer> ConsumerHarness =>
        _provider.GetRequiredService<ITestHarness>().GetConsumerHarness<ServiceCore.Audit.AuditConsumer>();

    /// <summary>
    /// Waits until the consumer has handled <paramref name="expected"/> messages
    /// and returns them. The harness only lists messages that were already
    /// consumed, so polling it is a deterministic wait for the consumer to run.
    /// </summary>
    public async Task<IReadOnlyList<HelloRequested>> WaitForConsumedAsync(int expected, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        var deadline = DateTime.UtcNow.Add(limit);

        while (DateTime.UtcNow < deadline)
        {
            var consumed = ConsumerHarness.Consumed
                .Select<HelloRequested>()
                .Select(x => x.Context.Message)
                .ToList();

            if (consumed.Count >= expected)
                return consumed;

            await Task.Delay(25);
        }

        var final = ConsumerHarness.Consumed
            .Select<HelloRequested>()
            .Select(x => x.Context.Message)
            .ToList();

        Assert.True(final.Count >= expected,
            $"Expected at least {expected} consumed message(s) but saw {final.Count} within {limit}.");

        return final;
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        Database.Dispose();
    }
}
