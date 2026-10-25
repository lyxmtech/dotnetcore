using Contracts;
using DataCore.Audit;
using DbCore;
using MassTransit;
using ServiceCore.Audit;

var builder = WebApplication.CreateBuilder(args);

// Single place where the EventBus flag is read (BusCore).
builder.Services.AddEventBus(builder.Configuration);

// Option B: real MassTransit consumer, wired through x.AddConsumer<T>().
// The broker wiring is only added when the EventBus flag selects RabbitMQ, so
// the service can also run standalone (EventBus=InMemory) without a broker.
if (EventBusSetup.ResolveMode(builder.Configuration) == EventBusMode.RabbitMq)
    builder.Services.AddRabbitMqBus(builder.Configuration, "audit", x => x.AddConsumer<AuditConsumer>());
else if (EventBusSetup.ResolveMode(builder.Configuration) == EventBusMode.Sqs)
    builder.Services.AddAmazonSqsBus(builder.Configuration, "audit", x => x.AddConsumer<AuditConsumer>());

// AuditService owns its own database file (database per service).
// The schema is created at startup; the file path comes from "Database__Path".
builder.Services.AddSqliteDatabase(builder.Configuration, AuditSchema.Statements);
builder.Services.AddSingleton<AuditRepository>();
builder.Services.AddSingleton<AuditServiceCore>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "audit", status = "up", eventBus = builder.Configuration["EventBus"] ?? "InMemory" }));

// Read side of the audit trail - proves the consumed events landed in SQLite.
app.MapGet("/audit", async (int? limit, AuditRepository repository) =>
{
    var rows = await repository.GetRecentAsync(limit ?? 50);
    return Results.Ok(new { count = rows.Count, audit = rows });
});

app.MapGet("/audit/stats", async (AuditRepository repository) =>
{
    var stats = await repository.GetStatsAsync();
    return Results.Ok(stats);
});

app.Run();

