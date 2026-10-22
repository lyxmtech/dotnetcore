namespace DataCore.Audit;

/// <summary>
/// Schema for the <c>audit</c> and <c>audit_stats</c> tables. It is owned by
/// AuditService: only AuditService (and DemoHost, which hosts it in-process)
/// may read or write these tables - that is the "database per service" rule.
/// <para>
/// The type lives in this shared class library so DemoHost can reuse the data
/// layer without referencing the Web SDK projects, whose appsettings*.json
/// content would otherwise collide during publish (NETSDK1152).
/// </para>
/// </summary>
public static class AuditSchema
{
    public const string Table = "audit";
    public const string StatsTable = "audit_stats";

    /// <summary>Idempotent DDL, executed at startup by DbCore's initializer.</summary>
    public static readonly string[] Statements =
    {
        """
        CREATE TABLE IF NOT EXISTS audit (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            name        TEXT    NOT NULL,
            requested_at TEXT   NOT NULL,
            recorded_at TEXT    NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS audit_stats (
            id         INTEGER PRIMARY KEY CHECK (id = 1),
            total      INTEGER NOT NULL DEFAULT 0,
            updated_at TEXT    NOT NULL
        );
        """,
        """
        INSERT INTO audit_stats (id, total, updated_at)
        VALUES (1, 0, '1970-01-01T00:00:00.0000000+00:00')
        ON CONFLICT(id) DO NOTHING;
        """,
        "CREATE INDEX IF NOT EXISTS ix_audit_recorded_at ON audit (recorded_at);"
    };
}

/// <summary>A row of the <c>audit</c> table.</summary>
public class AuditRow
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>The single-row counter table, updated inside the same transaction.</summary>
public class AuditStatsRow
{
    public long Id { get; set; }
    public long Total { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}