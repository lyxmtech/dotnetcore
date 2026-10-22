using System.Globalization;
using Dapper;
using DbCore;
using Microsoft.Extensions.Logging;

namespace DataCore.Requests;

/// <summary>
/// Dapper-based data access for the <c>requests</c> table.
/// Dapper maps columns to properties by name and maps parameters with the
/// ":name" syntax used by the SQL below.
/// <para>
/// It lives in this shared class library so DemoHost can reuse the data layer
/// without referencing the Web SDK projects, whose appsettings*.json content
/// would otherwise collide during publish (NETSDK1152).
/// </para>
/// </summary>
public class RequestsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<RequestsRepository> _logger;

    public RequestsRepository(IDbConnectionFactory connectionFactory, ILogger<RequestsRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>Inserts a request row and returns the generated id.</summary>
    public async Task<long> InsertAsync(string name, DateTimeOffset requestedAt, CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO requests (name, requested_at)
            VALUES (:Name, :RequestedAt);
            """;

        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new { Name = name, RequestedAt = requestedAt.ToString("O") },
            cancellationToken: cancellationToken));

        var id = await connection.QuerySingleAsync<long>(new CommandDefinition(
            "SELECT last_insert_rowid();",
            cancellationToken: cancellationToken));

        _logger.LogInformation("Stored request #{Id} for {Name}", id, name);
        return id;
    }

    /// <summary>Returns the most recent requests, newest first.</summary>
    public async Task<IReadOnlyList<RequestRow>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, name AS Name, requested_at AS RequestedAt
            FROM requests
            ORDER BY id DESC
            LIMIT @Limit;
            """;

        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);
        var rows = await connection.QueryAsync<RequestRowRecord>(new CommandDefinition(
            sql,
            new { Limit = limit },
            cancellationToken: cancellationToken));

        return rows.Select(r => new RequestRow
        {
            Id = r.Id,
            Name = r.Name,
            RequestedAt = ParseTimestamp(r.RequestedAt)
        }).ToList();
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    /// <summary>Raw row shape as stored in SQLite (timestamp kept as TEXT).</summary>
    private class RequestRowRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RequestedAt { get; set; } = string.Empty;
    }
}