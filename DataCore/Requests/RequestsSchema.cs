namespace DataCore.Requests;

/// <summary>
/// Schema for the <c>requests</c> table. It is owned by HelloService: only
/// HelloService (and DemoHost, which hosts it in-process) may read or write
/// these tables - that is the "database per service" rule.
/// <para>
/// The type lives in this shared class library so DemoHost can reuse the data
/// layer without referencing the Web SDK projects, whose appsettings*.json
/// content would otherwise collide during publish (NETSDK1152).
/// </para>
/// </summary>
public static class RequestsSchema
{
    public const string Table = "requests";

    /// <summary>Idempotent DDL, executed at startup by DbCore's initializer.</summary>
    public static readonly string[] Statements =
    {
        """
        CREATE TABLE IF NOT EXISTS requests (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            name         TEXT    NOT NULL,
            requested_at TEXT    NOT NULL
        );
        """,
        "CREATE INDEX IF NOT EXISTS ix_requests_requested_at ON requests (requested_at);"
    };
}

/// <summary>A row of the <c>requests</c> table.</summary>
public class RequestRow
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}