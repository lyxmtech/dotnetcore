using Amazon.SimpleNotificationService;
using Amazon.SQS;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

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

    /// <summary>
    /// Registers a single MassTransit bus using Amazon SQS/SNS. In AWS the endpoint
    /// and credentials come from the default credential chain (IAM task role); for
    /// local testing against LocalStack set Sqs__ServiceUrl (and optionally
    /// Sqs__AccessKey/Sqs__SecretKey).
    /// </summary>
    public static IServiceCollection AddAmazonSqsBus(
        this IServiceCollection services,
        IConfiguration configuration,
        string endpointPrefix,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        var options = configuration.GetSection(SqsOptions.SectionName).Get<SqsOptions>() ?? new SqsOptions();

        services.AddMassTransit(x =>
        {
            x.UsingAmazonSqs((ctx, cfg) =>
            {
                if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
                {
                    var serviceUri = new Uri(options.ServiceUrl);
                    var hostUri = new Uri($"amazonsqs://{serviceUri.Host}:{serviceUri.Port}");
                    cfg.Host(hostUri, h =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.AccessKey)) h.AccessKey(options.AccessKey);
                        if (!string.IsNullOrWhiteSpace(options.SecretKey)) h.SecretKey(options.SecretKey);
                        h.Config(new AmazonSQSConfig
                        {
                            ServiceURL = options.ServiceUrl,
                            AuthenticationRegion = options.Region
                        });
                        h.Config(new AmazonSimpleNotificationServiceConfig
                        {
                            ServiceURL = options.ServiceUrl,
                            AuthenticationRegion = options.Region
                        });
                    });
                }
                else
                {
                    cfg.Host(options.Region, h =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.AccessKey)) h.AccessKey(options.AccessKey);
                        if (!string.IsNullOrWhiteSpace(options.SecretKey)) h.SecretKey(options.SecretKey);
                    });
                }

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
        else if (mode == EventBusMode.Sqs)
            services.AddSingleton<IEventBus, SqsEventBus>();
        else
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        return services;
    }

    public static EventBusMode ResolveMode(IConfiguration configuration)
    {
        var raw = configuration["EventBus"]?.Trim();
        if (string.Equals(raw, "SQS", StringComparison.OrdinalIgnoreCase))
            return EventBusMode.Sqs;
        if (string.Equals(raw, "RabbitMQ", StringComparison.OrdinalIgnoreCase))
            return EventBusMode.RabbitMq;
        return EventBusMode.InMemory;
    }
}

public enum EventBusMode
{
    InMemory,
    RabbitMq,
    Sqs
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
            "Subscribe is only supported by the in-memory bus. With RabbitMQ or SQS, register an IConsumer<T> via x.AddConsumer<T>().");
    }
}
