using System.Globalization;
using Dapper;
using DbCore;
using Microsoft.Extensions.Logging;

namespace DataCore.Audit;

/// <summary>
/// Dapper-based data access for the <c>audit</c> and <c>audit_stats</c> tables.
/// <para>
/// It lives in this shared class library so DemoHost can reuse the data layer
/// without referencing the Web SDK projects, whose appsettings*.json content
/// would otherwise collide during publish (NETSDK1152).
/// </para>
/// </summary>
public class AuditRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<AuditRepository> _logger;

    public AuditRepository(IDbConnectionFactory connectionFactory, ILogger<AuditRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    /// Records an audited event and bumps the counter in a single transaction, so a
    /// failure can never leave the two out of sync. Dapper runs the statements on
    /// the transaction's connection.
    /// </summary>
    public async Task<long> RecordAsync(string name, DateTimeOffset requestedAt, DateTimeOffset recordedAt, CancellationToken cancellationToken = default)
    {
        const string insertAudit = """
            INSERT INTO audit (name, requested_at, recorded_at)
            VALUES (:Name, :RequestedAt, :RecordedAt);
            """;

        const string bumpStats = """
            UPDATE audit_stats
            SET total = total + 1, updated_at = :RecordedAt
            WHERE id = 1;
            """;

        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertAudit,
                new { Name = name, RequestedAt = requestedAt.ToString("O"), RecordedAt = recordedAt.ToString("O") },
                transaction,
                cancellationToken: cancellationToken));

            var id = await connection.QuerySingleAsync<long>(new CommandDefinition(
                "SELECT last_insert_rowid();",
                transaction: transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                bumpStats,
                new { RecordedAt = recordedAt.ToString("O") },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Audited event #{Id} for {Name}", id, name);
            return id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Returns the most recent audited events, newest first.</summary>
    public async Task<IReadOnlyList<AuditRow>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, name AS Name, requested_at AS RequestedAt, recorded_at AS RecordedAt
            FROM audit
            ORDER BY id DESC
            LIMIT @Limit;
            """;

        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuditRowRecord>(new CommandDefinition(
            sql,
            new { Limit = limit },
            cancellationToken: cancellationToken));

        return rows.Select(r => new AuditRow
        {
            Id = r.Id,
            Name = r.Name,
            RequestedAt = ParseTimestamp(r.RequestedAt),
            RecordedAt = ParseTimestamp(r.RecordedAt)
        }).ToList();
    }

    /// <summary>Returns the counter row maintained by <see cref="RecordAsync"/>.</summary>
    public async Task<AuditStatsRow> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id AS Id, total AS Total, updated_at AS UpdatedAt FROM audit_stats WHERE id = 1;";

        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<StatsRowRecord>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken));

        if (row is null)
            return new AuditStatsRow { Id = 1, Total = 0, UpdatedAt = DateTimeOffset.MinValue };

        return new AuditStatsRow
        {
            Id = row.Id,
            Total = row.Total,
            UpdatedAt = ParseTimestamp(row.UpdatedAt)
        };
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    /// <summary>Raw row shape as stored in SQLite (timestamps kept as TEXT).</summary>
    private class AuditRowRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RequestedAt { get; set; } = string.Empty;
        public string RecordedAt { get; set; } = string.Empty;
    }

    private class StatsRowRecord
    {
        public long Id { get; set; }
        public long Total { get; set; }
        public string UpdatedAt { get; set; } = string.Empty;
    }
}