using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceTests;

public class EventBusSetupTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        var builder = new ConfigurationBuilder();
        foreach (var (key, value) in values)
            builder.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value });
        return builder.Build();
    }

    [Theory]
    [InlineData("RabbitMQ", EventBusMode.RabbitMq)]
    [InlineData("rabbitmq", EventBusMode.RabbitMq)]
    [InlineData("RABBITMQ", EventBusMode.RabbitMq)]
    [InlineData("  RabbitMQ  ", EventBusMode.RabbitMq)]
    [InlineData("InMemory", EventBusMode.InMemory)]
    [InlineData("inmemory", EventBusMode.InMemory)]
    [InlineData("SomethingElse", EventBusMode.InMemory)]
    [InlineData("", EventBusMode.InMemory)]
    public void ResolveMode_ReadsTheEventBusFlag(string raw, EventBusMode expected)
    {
        Assert.Equal(expected, EventBusSetup.ResolveMode(Configuration(("EventBus", raw))));
    }

    [Fact]
    public void ResolveMode_MissingFlag_DefaultsToInMemory()
    {
        Assert.Equal(EventBusMode.InMemory, EventBusSetup.ResolveMode(Configuration()));
    }

    [Fact]
    public void AddEventBus_RabbitMqFlag_RegistersTheRabbitMqBus()
    {
        var configuration = Configuration(("EventBus", "RabbitMQ"));

        var services = new ServiceCollection();
        services.AddEventBus(configuration);
        // The transport is what supplies IPublishEndpoint to RabbitMqEventBus.
        services.AddRabbitMqBus(configuration, "test");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RabbitMqEventBus>(provider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public void AddEventBus_InMemoryFlag_RegistersTheInMemoryBus()
    {
        var services = new ServiceCollection();
        services.AddEventBus(Configuration(("EventBus", "InMemory")));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<InMemoryEventBus>(provider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public void AddEventBus_MissingFlag_RegistersTheInMemoryBus()
    {
        var services = new ServiceCollection();
        services.AddEventBus(Configuration());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<InMemoryEventBus>(provider.GetRequiredService<IEventBus>());
    }

    [Fact]
    public void AddEventBus_RegistersTheBusAsASingleton()
    {
        var services = new ServiceCollection();
        services.AddEventBus(Configuration(("EventBus", "InMemory")));

        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IEventBus>(), provider.GetRequiredService<IEventBus>());
    }
}
