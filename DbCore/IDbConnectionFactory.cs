using Microsoft.Data.Sqlite;

namespace DbCore;

/// <summary>
/// Creates configured SQLite connections. Dapper works against any
/// <see cref="System.Data.Common.DbConnection"/>, so repositories only depend on
/// this factory and never on the concrete provider.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Opens a new connection to the service's own SQLite file.</summary>
    Task<SqliteConnection> CreateAsync(CancellationToken cancellationToken = default);
}
