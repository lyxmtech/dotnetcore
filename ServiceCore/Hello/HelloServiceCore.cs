using Contracts;
using DataCore.Requests;
using Microsoft.Extensions.Logging;

namespace ServiceCore.Hello;

/// <summary>
/// The behaviour behind HelloService's <c>GET /hello</c> endpoint: persist the
/// request in this service's own database and then publish the event that the
/// other services consume.
/// <para>
/// It lives in this shared class library so the behaviour can be unit tested
/// without starting an HTTP host. The web project (HelloService) only maps the
/// endpoint and calls into this class.
/// </para>
/// </summary>
public class HelloServiceCore
{
    /// <summary>Name used when the caller does not supply one.</summary>
    public const string DefaultName = "World";

    /// <summary>
    /// Normalises a requested name: blank or whitespace becomes
    /// <see cref="DefaultName"/>. This is the single place where that rule
    /// lives, so the endpoint and the stored row always agree.
    /// </summary>
    public static string ResolveName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? DefaultName : name;

    private readonly IEventBus _bus;
    private readonly RequestsRepository _repository;
    private readonly ILogger<HelloServiceCore> _logger;

    public HelloServiceCore(IEventBus bus, RequestsRepository repository, ILogger<HelloServiceCore> logger)
    {
        _bus = bus;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Handles a hello request: stores the request row and publishes
    /// <see cref="HelloRequested"/>. Returns the id of the stored row.
    /// </summary>
    /// <param name="name">Requested name; blank or whitespace means <see cref="DefaultName"/>.</param>
    /// <param name="requestedAt">Timestamp of the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HelloResult> HandleHelloAsync(
        string? name,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new HelloRequested(ResolveName(name), requestedAt);

        // 1) durable side effect in this service's own database
        var id = await _repository.InsertAsync(evt.Name, evt.At, cancellationToken);

        // 2) then publish the event to the other services
        _logger.LogInformation("Publishing HelloRequested for {Name} at {At} (request #{Id})", evt.Name, evt.At, id);
        await _bus.PublishAsync(evt);

        return new HelloResult(id, evt.Name);
    }
}

/// <summary>The outcome of a hello request: the stored row id and the name used.</summary>
public record HelloResult(long RequestId, string Name);
