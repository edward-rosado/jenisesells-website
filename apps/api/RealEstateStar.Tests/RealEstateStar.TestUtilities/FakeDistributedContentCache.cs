using System.Collections.Concurrent;
using System.Text.Json;
using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.TestUtilities;

/// <summary>
/// In-memory <see cref="IDistributedContentCache"/> implementation for unit tests.
/// Stores values serialized as JSON (same round-trip as the real Table Storage impl)
/// and respects TTL. Thread-safe via <see cref="ConcurrentDictionary"/>.
/// </summary>
public sealed class FakeDistributedContentCache : IDistributedContentCache
{
    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset ExpiresAt)> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        if (_store.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            var value = JsonSerializer.Deserialize<T>(entry.Json);
            return Task.FromResult(value);
        }
        return Task.FromResult((T?)null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(value);
        _store[key] = (json, DateTimeOffset.UtcNow + ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>Returns the number of currently non-expired entries in the cache.</summary>
    public int Count
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return _store.Count(e => e.Value.ExpiresAt > now);
        }
    }
}
