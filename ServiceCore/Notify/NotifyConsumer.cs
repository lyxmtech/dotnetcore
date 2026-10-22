using Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ServiceCore.Notify;

/// <summary>
/// Consumes <see cref="HelloRequested"/> and "notifies" the user. It has no
/// database of its own - notifying is a side effect without durable state.
/// <para>
/// It lives in this shared class library (rather than in the NotifyService web
/// project) so the consumer can be unit tested with an in-memory MassTransit
/// harness. A class library ships no appsettings*.json, so referencing it can
/// never trigger the NETSDK1152 duplicate-content error.
/// </para>
/// </summary>
public class NotifyConsumer : IConsumer<HelloRequested>
{
    private readonly ILogger<NotifyConsumer> _logger;

    public NotifyConsumer(ILogger<NotifyConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<HelloRequested> ctx)
    {
        _logger.LogInformation("[NOTIFY] Sending welcome notification to {Name}", ctx.Message.Name);
        Console.WriteLine($"[NOTIFY] 👋 Sending welcome notification to {ctx.Message.Name}");
        return Task.CompletedTask;
    }
}
