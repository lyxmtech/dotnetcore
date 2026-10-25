using MassTransit;

namespace Contracts;

/// <summary>
/// Option C: MassTransit-backed event bus over Amazon SQS/SNS. Publishes to SNS topics
/// so consumers in other processes receive the event. Same shape as RabbitMqEventBus.
/// </summary>
public class SqsEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;

    public SqsEventBus(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<T>(T evt) where T : class
    {
        return _publishEndpoint.Publish(evt);
    }
}