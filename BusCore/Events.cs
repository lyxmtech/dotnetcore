using System.Collections.Concurrent;

namespace Contracts;

// The event published by HelloService and consumed by AuditService/NotifyService.
public record HelloRequested(string Name, DateTimeOffset At);

// Abstraction over the two bus implementations so services do not depend on MassTransit directly.
public interface IEventBus
{
    Task PublishAsync<T>(T evt) where T : class;
}

// Option A: in-process event bus (no broker). Each process has its own bus,
// so a publish from one process will NOT reach subscribers in another.
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<object, Task>>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe<T>(Func<T, Task> handler) where T : class
    {
        var list = _handlers.GetOrAdd(typeof(T), _ => new List<Func<object, Task>>());
        lock (_lock) list.Add(o => handler((T)o));
    }

    public async Task PublishAsync<T>(T evt) where T : class
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers)) return;
        List<Func<object, Task>> snapshot;
        lock (_lock) snapshot = new List<Func<object, Task>>(handlers);
        foreach (var h in snapshot) await h(evt);
    }
}
