using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.TestUtilities;

/// <summary>
/// In-memory <see cref="IContentCache"/> implementation for unit tests.
/// Stores values in a dictionary and respects TTL by recording expiry.
/// Thread-safe via lock for concurrent test scenarios.
/// </summary>
public sealed class FakeContentCache : IContentCache
{
    private readonly Dictionary<string, (object Value, DateTime ExpiresAt)> _store = new();
    private readonly object _lock = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (_store.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
                return Task.FromResult((T?)entry.Value);
        }
        return Task.FromResult((T?)null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _store[key] = (value, DateTime.UtcNow.Add(ttl));
        }
        return Task.CompletedTask;
    }

    /// <summary>Returns the number of currently non-expired entries in the cache.</summary>
    public int Count
    {
        get
        {
            var now = DateTime.UtcNow;
            lock (_lock)
                return _store.Count(e => e.Value.ExpiresAt > now);
        }
    }
}
