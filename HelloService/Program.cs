using Contracts;
using DataCore.Requests;
using DbCore;
using MassTransit;
using ServiceCore.Hello;

var builder = WebApplication.CreateBuilder(args);

// Single place where the EventBus flag is read (BusCore).
builder.Services.AddEventBus(builder.Configuration);

// Option B: publish goes to RabbitMQ. HelloService does not consume.
// The broker wiring is only added when the EventBus flag selects RabbitMQ, so
// the service can also run standalone (EventBus=InMemory) without a broker.
if (EventBusSetup.ResolveMode(builder.Configuration) == EventBusMode.RabbitMq)
    builder.Services.AddRabbitMqBus(builder.Configuration, "hello");
else if (EventBusSetup.ResolveMode(builder.Configuration) == EventBusMode.Sqs)
    builder.Services.AddAmazonSqsBus(builder.Configuration, "hello");

// HelloService owns its own database file (database per service).
// The schema is created at startup; the file path comes from "Database__Path".
builder.Services.AddSqliteDatabase(builder.Configuration, RequestsSchema.Statements);
builder.Services.AddSingleton<RequestsRepository>();

// The request handling logic lives in ServiceCore so it can be unit tested.
builder.Services.AddSingleton<HelloServiceCore>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "hello", status = "up", eventBus = builder.Configuration["EventBus"] ?? "InMemory" }));

app.MapGet("/hello", async (string? name, HelloServiceCore core) =>
{
    var result = await core.HandleHelloAsync(name, DateTimeOffset.UtcNow);
    return Results.Ok(new { message = $"Hello, {result.Name}!", requestId = result.RequestId });
});

// Read side of the same database - proves the rows really landed in SQLite.
app.MapGet("/requests", async (int? limit, RequestsRepository repository) =>
{
    var rows = await repository.GetRecentAsync(limit ?? 50);
    return Results.Ok(new { count = rows.Count, requests = rows });
});

app.Run();

