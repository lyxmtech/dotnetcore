using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DbCore;

/// <summary>
/// Builds a <see cref="SqliteConnection"/> from the "Database" configuration section.
/// The file path is resolved once at construction time, so every connection in the
/// process points at the same database.
/// </summary>
public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly int _busyTimeoutSeconds;

    public SqliteConnectionFactory(IOptions<DatabaseOptions> options)
    {
        var opts = options.Value;
        _busyTimeoutSeconds = Math.Max(0, opts.BusyTimeoutSeconds);
        _connectionString = BuildConnectionString(opts.Path);
    }

    /// <summary>The resolved connection string, e.g. "Data Source=/data/hello.db".</summary>
    public string ConnectionString => _connectionString;

    public async Task<SqliteConnection> CreateAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // WAL lets readers work while a writer holds the lock; busy_timeout makes
        // a concurrent writer wait instead of failing immediately with SQLITE_BUSY.
        await ExecutePragmaAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
        await ExecutePragmaAsync(connection, $"PRAGMA busy_timeout={_busyTimeoutSeconds * 1000};", cancellationToken);
        await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=ON;", cancellationToken);

        return connection;
    }

    private static async Task ExecutePragmaAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildConnectionString(string? path)
    {
        // A full connection string can be supplied if desired - use it verbatim.
        if (!string.IsNullOrWhiteSpace(path))
        {
            var trimmed = path.Trim();
            if (trimmed.Contains('=', StringComparison.Ordinal) &&
                (trimmed.StartsWith("Data Source", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("DataSource", StringComparison.OrdinalIgnoreCase)))
            {
                EnsureDirectoryExists(ExtractDataSource(trimmed));
                return trimmed;
            }
        }

        var filePath = ResolveFilePath(path);
        EnsureDirectoryExists(filePath);
        return $"Data Source={filePath}";
    }

    /// <summary>
    /// Turns a configured path into an absolute file path. Relative paths
    /// ("data/hello.db") are resolved against the current working directory.
    /// </summary>
    private static string ResolveFilePath(string? path)
    {
        var raw = string.IsNullOrWhiteSpace(path) ? "db/app.db" : path.Trim();

        // A URI-style file name ("file:app.db?mode=memory") is passed through as-is.
        if (raw.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return raw;

        return Path.GetFullPath(raw);
    }

    /// <summary>Pulls the file path out of a "Data Source=..." connection string.</summary>
    private static string ExtractDataSource(string connectionString)
    {
        var index = connectionString.IndexOf('=');
        return index < 0 ? connectionString : connectionString[(index + 1)..].Trim();
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        if (filePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}
