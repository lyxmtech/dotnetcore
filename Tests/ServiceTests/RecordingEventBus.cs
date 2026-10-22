using Contracts;
using Xunit;

namespace ServiceTests;

/// <summary>
/// An <see cref="IEventBus"/> that records everything published to it, so a
/// test can assert that an event really was published (and what it carried)
/// without needing a broker.
/// </summary>
public sealed class RecordingEventBus : IEventBus
{
    private readonly List<object> _published = new();
    private readonly object _lock = new();

    /// <summary>All published events, in publication order.</summary>
    public IReadOnlyList<object> Published
    {
        get
        {
            lock (_lock) return _published.ToList();
        }
    }

    /// <summary>Number of published events.</summary>
    public int Count
    {
        get
        {
            lock (_lock) return _published.Count;
        }
    }

    public Task PublishAsync<T>(T evt) where T : class
    {
        lock (_lock) _published.Add(evt);
        return Task.CompletedTask;
    }

    /// <summary>Returns the single published event of type <typeparamref name="T"/>.</summary>
    public T Single<T>() where T : class
    {
        return Assert.Single(Published.OfType<T>().ToList());
    }

    /// <summary>Returns all published events of type <typeparamref name="T"/>.</summary>
    public IReadOnlyList<T> All<T>() where T : class
    {
        return Published.OfType<T>().ToList();
    }
}
