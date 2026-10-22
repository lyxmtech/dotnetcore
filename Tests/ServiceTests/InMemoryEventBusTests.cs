using Contracts;
using MassTransit;
using MassTransit.Util;
using Xunit;

namespace ServiceTests;

public class InMemoryEventBusTests
{
    [Fact]
    public async Task PublishAsync_WithSubscriber_InvokesTheHandler()
    {
        var bus = new InMemoryEventBus();
        HelloRequested? received = null;
        bus.Subscribe<HelloRequested>(e =>
        {
            received = e;
            return Task.CompletedTask;
        });

        var evt = new HelloRequested("Alice", DateTimeOffset.UtcNow);
        await bus.PublishAsync(evt);

        Assert.NotNull(received);
        Assert.Equal("Alice", received!.Name);
    }

    [Fact]
    public async Task PublishAsync_FansOutToEverySubscriber()
    {
        // This is the behaviour that makes Option A work: one bus, many subscribers.
        var bus = new InMemoryEventBus();
        var audit = 0;
        var notify = 0;

        bus.Subscribe<HelloRequested>(_ => { audit++; return Task.CompletedTask; });
        bus.Subscribe<HelloRequested>(_ => { notify++; return Task.CompletedTask; });

        await bus.PublishAsync(new HelloRequested("Alice", DateTimeOffset.UtcNow));

        Assert.Equal(1, audit);
        Assert.Equal(1, notify);
    }

    [Fact]
    public async Task PublishAsync_WithoutSubscriber_DoesNothing()
    {
        var bus = new InMemoryEventBus();

        await bus.PublishAsync(new HelloRequested("Alice", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task PublishAsync_OnlyNotifiesSubscribersOfTheSameType()
    {
        var bus = new InMemoryEventBus();
        var audit = 0;
        bus.Subscribe<HelloRequested>(_ => { audit++; return Task.CompletedTask; });

        await bus.PublishAsync(new OtherEvent());

        Assert.Equal(0, audit);
    }

    [Fact]
    public async Task Subscribe_AddedAfterPublish_OnlySeesLaterEvents()
    {
        var bus = new InMemoryEventBus();
        var count = 0;

        await bus.PublishAsync(new HelloRequested("before", DateTimeOffset.UtcNow));
        bus.Subscribe<HelloRequested>(_ => { count++; return Task.CompletedTask; });
        await bus.PublishAsync(new HelloRequested("after", DateTimeOffset.UtcNow));

        Assert.Equal(1, count);
    }

    [Fact]
    public void Subscribe_Throws_WhenTheBusIsNotTheInMemoryOne()
    {
        // RabbitMQ delivery is handled by MassTransit consumers, not by Subscribe.
        var bus = new RabbitMqEventBus(new NoopPublishEndpoint());

        var ex = Assert.Throws<NotSupportedException>(() =>
            bus.Subscribe<HelloRequested>(_ => Task.CompletedTask));

        Assert.Contains("in-memory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record OtherEvent;

    /// <summary>
    /// A publish endpoint that accepts everything and publishes nothing. Only
    /// the fact that <see cref="RabbitMqEventBus"/> needs an endpoint matters
    /// for the Subscribe test above.
    /// </summary>
    private sealed class NoopPublishEndpoint : IPublishEndpoint, IPublishObserverConnector
    {
        public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => new EmptyConnectHandle();
        public Task Publish<T>(T message, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(T message, IPipe<PublishContext<T>> pipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(T message, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish(object message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish(object message, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish(object message, Type messageType, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<T>(object message, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(object message, IPipe<PublishContext<T>> pipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(object message, IPipe<PublishContext> pipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;
    }
}
