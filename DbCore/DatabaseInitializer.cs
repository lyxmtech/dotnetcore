using Dapper;
using Microsoft.Extensions.Logging;

namespace DbCore;

/// <summary>
/// Runs the service's DDL at startup. Every statement must be idempotent
/// (CREATE TABLE IF NOT EXISTS), so restarting the service is always safe.
/// </summary>
public class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(string[] schemaStatements, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateAsync(cancellationToken);

        foreach (var statement in schemaStatements)
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;

            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var tables = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;",
            cancellationToken: cancellationToken));

        _logger.LogInformation("SQLite schema ready. Tables: {Tables}", string.Join(", ", tables));
    }
}
