using DataCore.Audit;
using Microsoft.Extensions.Logging;

namespace ServiceCore.Audit;

/// <summary>
/// The behaviour behind AuditService: recording a consumed <c>HelloRequested</c>
/// event into this service's own database.
/// <para>
/// It lives in this shared class library so the behaviour can be unit tested.
/// Both the MassTransit consumer (AuditService, Option B) and the in-process
/// subscriber (DemoHost, Option A) go through this one class, so they can
/// never drift apart.
/// </para>
/// </summary>
public class AuditServiceCore
{
    private readonly AuditRepository _repository;
    private readonly ILogger<AuditServiceCore> _logger;

    public AuditServiceCore(AuditRepository repository, ILogger<AuditServiceCore> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Records an audited event and bumps the counter in a single transaction.
    /// </summary>
    /// <param name="name">Name from the event.</param>
    /// <param name="requestedAt">When the event was originally requested.</param>
    /// <param name="recordedAt">When the event was recorded; defaults to now.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The id of the new audit row.</returns>
    public Task<long> RecordAsync(
        string name,
        DateTimeOffset requestedAt,
        DateTimeOffset? recordedAt = null,
        CancellationToken cancellationToken = default)
    {
        var stamp = recordedAt ?? DateTimeOffset.UtcNow;
        _logger.LogInformation("[AUDIT] {At:O} hello requested by {Name}", requestedAt, name);
        return _repository.RecordAsync(name, requestedAt, stamp, cancellationToken);
    }
}
