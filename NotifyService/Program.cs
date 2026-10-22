using Contracts;
using MassTransit;
using ServiceCore.Notify;

var builder = WebApplication.CreateBuilder(args);

// Single place where the EventBus flag is read (BusCore).
builder.Services.AddEventBus(builder.Configuration);

// Option B: real MassTransit consumer, wired through x.AddConsumer<T>().
// The broker wiring is only added when the EventBus flag selects RabbitMQ, so
// the service can also run standalone (EventBus=InMemory) without a broker.
if (EventBusSetup.ResolveMode(builder.Configuration) == EventBusMode.RabbitMq)
    builder.Services.AddRabbitMqBus(builder.Configuration, "notify", x => x.AddConsumer<NotifyConsumer>());

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "notify", status = "up", eventBus = builder.Configuration["EventBus"] ?? "InMemory" }));

app.Run();

