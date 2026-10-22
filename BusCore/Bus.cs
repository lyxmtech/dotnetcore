using MassTransit;

namespace Contracts;

/// <summary>
/// Option B: MassTransit-backed event bus. Publishes to RabbitMQ so that
/// consumers in other processes receive the event.
/// </summary>
public class RabbitMqEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqEventBus(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<T>(T evt) where T : class
    {
        return _publishEndpoint.Publish(evt);
    }
}
