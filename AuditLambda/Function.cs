using Amazon.Lambda.Core;
using Microsoft.Data.Sqlite;
using Dapper;

// The default Lambda serializer (source-generated) is used when the attribute is omitted.
// If you prefer the System.Text.Json serializer, you can add the attribute back later.
namespace AuditLambda;

public class AuditCountResponse
{
    public long Total { get; set; }
}

public class Function
{
    /// <summary>
    /// Returns the number of records in the audit table.
    /// The database path is read from the environment variable "AUDIT_DB_PATH";
    /// defaults to "audit.db" if not set. If the file does not exist, the function
    /// creates the required schema (audit + audit_stats tables) so it works out‑of‑the‑box.
    /// </summary>
    /// <param name="input">Ignored; kept for Lambda signature compatibility.</param>
    /// <param name="context">Ignored; kept for Lambda signature compatibility.</param>
    /// <returns>The total count of audit records.</returns>
    public async Task<AuditCountResponse> FunctionHandlerAsync(Stream input, ILambdaContext context)
    {
        // Read the database path from an environment variable; you can set this in the Lambda console
        // or in your serverless configuration.
        var dbPath = Environment.GetEnvironmentVariable("AUDIT_DB_PATH") ?? "audit.db";

        // Ensure the database and required tables exist.
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();

            // Create the schema if the tables are missing.
            const string createAudit = @"
                CREATE TABLE IF NOT EXISTS audit (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    requested_at TEXT NOT NULL,
                    recorded_at TEXT NOT NULL
                );";
            const string createStats = @"
                CREATE TABLE IF NOT EXISTS audit_stats (
                    id INTEGER PRIMARY KEY,
                    total INTEGER DEFAULT 0,
                    updated_at TEXT
                );";
            await connection.ExecuteAsync(createAudit);
            await connection.ExecuteAsync(createStats);

            // Ensure a stats row exists (id = 1) so we can always read a total.
            const string upsertStats = @"
                INSERT INTO audit_stats (id, total, updated_at)
                VALUES (1, 0, CAST(strftime('%Y-%m-%dT%H:%M:%fZ', 'now') AS TEXT))
                ON CONFLICT(id) DO UPDATE SET total = total, updated_at = CAST(strftime('%Y-%m-%dT%H:%M:%fZ', 'now') AS TEXT);";
            await connection.ExecuteAsync(upsertStats);

            // Count rows in the audit table.
            const string countSql = "SELECT COUNT(*) AS Total FROM audit";
            var total = await connection.ExecuteScalarAsync<long>(countSql);

            return new AuditCountResponse { Total = total };
        }
    }
}
