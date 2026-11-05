namespace DbCore;

/// <summary>
/// SQLite connection settings, bound from the "Database" configuration section.
/// Env var form: Database__Path, Database__BusyTimeoutSeconds.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Path of the SQLite file, e.g. "/data/hello.db" in a container or
    /// "./db/hello.db" when running locally. The directory is created on startup.
    /// </summary>
    public string Path { get; set; } = "db/app.db";

    /// <summary>
    /// Journal mode used by SQLite. WAL is the default for local filesystems;
    /// use DELETE for databases shared across hosts on a network filesystem.
    /// </summary>
    public string JournalMode { get; set; } = "WAL";

    /// <summary>
    /// How long (seconds) SQLite waits for a competing writer before failing with
    /// SQLITE_BUSY. Only matters when several requests write concurrently.
    /// </summary>
    public int BusyTimeoutSeconds { get; set; } = 5;
}
