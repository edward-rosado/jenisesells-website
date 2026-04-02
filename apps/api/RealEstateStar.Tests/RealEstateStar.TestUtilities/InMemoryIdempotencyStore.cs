using System.Collections.Concurrent;
using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.TestUtilities;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentHashSet _completed = new();

    public Task<bool> HasCompletedAsync(string key, CancellationToken ct) =>
        Task.FromResult(_completed.Contains(key));

    public Task MarkCompletedAsync(string key, CancellationToken ct)
    {
        _completed.Add(key);
        return Task.CompletedTask;
    }

    private sealed class ConcurrentHashSet
    {
        private readonly ConcurrentDictionary<string, byte> _dict = new();

        public bool Contains(string key) => _dict.ContainsKey(key);
        public void Add(string key) => _dict.TryAdd(key, 0);
    }
}
