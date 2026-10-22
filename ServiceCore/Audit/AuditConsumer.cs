using Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ServiceCore.Audit;

/// <summary>
/// Consumes <see cref="HelloRequested"/> and writes an audit row into
/// AuditService's own SQLite database.
/// <para>
/// It lives in this shared class library (rather than in the AuditService web
/// project) so the consumer can be unit tested with an in-memory MassTransit
/// harness. A class library ships no appsettings*.json, so referencing it can
/// never trigger the NETSDK1152 duplicate-content error.
/// </para>
/// </summary>
public class AuditConsumer : IConsumer<HelloRequested>
{
    private readonly AuditServiceCore _service;
    private readonly ILogger<AuditConsumer> _logger;

    public AuditConsumer(AuditServiceCore service, ILogger<AuditConsumer> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<HelloRequested> ctx)
    {
        _logger.LogInformation("[AUDIT] {At:O} hello requested by {Name}", ctx.Message.At, ctx.Message.Name);
        Console.WriteLine($"[AUDIT] {ctx.Message.At:O} hello requested by {ctx.Message.Name}");

        // Durable side effect: the event is written to this service's own SQLite
        // database. If the write throws, MassTransit retries the message instead
        // of acknowledging an event that was never persisted.
        await _service.RecordAsync(ctx.Message.Name, ctx.Message.At);
    }
}
