using Contracts;
using DataCore.Audit;
using DataCore.Requests;
using DbCore;
using ServiceCore.Audit;
using ServiceCore.Hello;

// DemoHost (Option A+): hosts Hello + Audit + Notify in ONE process so the
// singleton InMemoryEventBus is shared and PublishAsync actually fans out.
// Run this for the working local demo. Run the three *Service projects
// separately only to show the per-process limitation (their buses are isolated).
//
// Option A uses ONE shared database file (demo.db) holding both the `requests`
// and the `audit` tables. Option B (separate containers) gives each service its
// own file - that is the "database per service" rule in its purest form.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEventBus(builder.Configuration);

// One database, two schemas: both DDL statements are applied at startup.
builder.Services.AddSqliteDatabase(builder.Configuration,
    RequestsSchema.Statements.Concat(AuditSchema.Statements).ToArray());
builder.Services.AddSingleton<RequestsRepository>();
builder.Services.AddSingleton<AuditRepository>();

// The request and audit logic live in ServiceCore so it can be unit tested.
builder.Services.AddSingleton<HelloServiceCore>();
builder.Services.AddSingleton<AuditServiceCore>();

var app = builder.Build();

// Subscriber #1 - Audit: writes the consumed event into the shared database.
var bus = app.Services.GetRequiredService<IEventBus>();
var auditService = app.Services.GetRequiredService<AuditServiceCore>();
bus.Subscribe<HelloRequested>(async e =>
{
    Console.WriteLine($"[AUDIT] {e.At:O} hello requested by {e.Name}");
    await auditService.RecordAsync(e.Name, e.At);
});

// Subscriber #2 - Notify: no database needed, it only sends a notification.
bus.Subscribe<HelloRequested>(e =>
{
    Console.WriteLine($"[NOTIFY] Sending welcome notification to {e.Name}");
    return Task.CompletedTask;
});

app.MapGet("/", () => Results.Ok(new { service = "demo-host", status = "up", mode = "single-process-shared-bus" }));

app.MapGet("/hello", async (string? name, HelloServiceCore core) =>
{
    var result = await core.HandleHelloAsync(name, DateTimeOffset.UtcNow);
    return Results.Ok(new { message = $"Hello, {result.Name}!", requestId = result.RequestId });
});

// Read sides - both tables live in the same demo.db file.
app.MapGet("/requests", async (int? limit, RequestsRepository requests) =>
{
    var rows = await requests.GetRecentAsync(limit ?? 50);
    return Results.Ok(new { count = rows.Count, requests = rows });
});

app.MapGet("/audit", async (int? limit, AuditRepository audit) =>
{
    var rows = await audit.GetRecentAsync(limit ?? 50);
    return Results.Ok(new { count = rows.Count, audit = rows });
});

app.MapGet("/audit/stats", async (AuditRepository audit) => Results.Ok(await audit.GetStatsAsync()));

app.Run();
