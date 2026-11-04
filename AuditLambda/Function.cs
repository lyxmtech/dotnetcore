using Amazon.Lambda.Core;
using Microsoft.Data.Sqlite;
using Dapper;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

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
    /// defaults to "audit.db" if not set. The file must already exist and contain
    /// the schema managed by AuditService.
    /// </summary>
    /// <param name="input">Lambda event payload, which this function does not use.</param>
    /// <param name="context">Lambda invocation context.</param>
    /// <returns>The total count of audit records.</returns>
    public async Task<AuditCountResponse> FunctionHandlerAsync(object input, ILambdaContext context)
    {
        var dbPath = Environment.GetEnvironmentVariable("AUDIT_DB_PATH") ?? "audit.db";
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException(
                $"The audit database was not found at '{dbPath}'. Configure AUDIT_DB_PATH to an existing database accessible to the Lambda function.",
                dbPath);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        const string countSql = "SELECT COUNT(*) FROM audit";
        var total = await connection.ExecuteScalarAsync<long>(countSql);

        return new AuditCountResponse { Total = total };
    }
}