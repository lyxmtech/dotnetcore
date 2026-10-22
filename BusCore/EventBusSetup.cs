using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Contracts;

/// <summary>
/// Shared MassTransit wiring for every service.
/// </summary>
public static class MassTransitSetup
{
    /// <summary>
    /// Registers a single MassTransit bus using RabbitMQ. The broker host and credentials
    /// come from the "RabbitMq" configuration section, so the same image runs under
    /// Docker Compose (host: rabbitmq) and Kubernetes (host: rabbitmq Service DNS name)
    /// without a rebuild.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration ("RabbitMq" section).</param>
    /// <param name="endpointPrefix">Prefix used to name the consumer endpoint queue.</param>
    /// <param name="configureBus">
    /// Optional callback on the same bus factory context, used to register consumers
    /// (for example <c>x.AddConsumer&lt;AuditConsumer&gt;()</c>). Consumers must be added on
    /// the same context as the transport, so they are passed in here rather than via a
    /// second <c>AddMassTransit</c> call.
    /// </param>
    public static IServiceCollection AddRabbitMqBus(
        this IServiceCollection services,
        IConfiguration configuration,
        string endpointPrefix,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });
                cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(endpointPrefix, false));
            });

            configureBus?.Invoke(x);
        });

        return services;
    }
}

public static class EventBusSetup
{
    /// <summary>
    /// Single place where the EventBus flag is interpreted.
    /// "RabbitMQ" (or any value other than "InMemory") selects MassTransit/RabbitMQ;
    /// "InMemory" selects the in-process bus.
    /// </summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = ResolveMode(configuration);
        if (mode == EventBusMode.RabbitMq)
            services.AddSingleton<IEventBus, RabbitMqEventBus>();
        else
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        return services;
    }

    public static EventBusMode ResolveMode(IConfiguration configuration)
    {
        var raw = configuration["EventBus"]?.Trim();
        return string.Equals(raw, "RabbitMQ", StringComparison.OrdinalIgnoreCase)
            ? EventBusMode.RabbitMq
            : EventBusMode.InMemory;
    }
}

public enum EventBusMode
{
    InMemory,
    RabbitMq
}

/// <summary>
/// Subscribe helper used by the in-process demo (DemoHost).
/// RabbitMQ delivery is handled by MassTransit consumers instead, so
/// subscribing is only supported on the in-memory bus.
/// </summary>
public static class EventBusExtensions
{
    public static void Subscribe<T>(this IEventBus bus, Func<T, Task> handler) where T : class
    {
        if (bus is InMemoryEventBus inMemory)
        {
            inMemory.Subscribe(handler);
            return;
        }

        throw new NotSupportedException(
            "Subscribe is only supported by the in-memory bus. With RabbitMQ, register an IConsumer<T> via x.AddConsumer<T>().");
    }
}
