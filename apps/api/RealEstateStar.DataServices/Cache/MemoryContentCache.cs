using Microsoft.Extensions.Caching.Memory;
using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.DataServices.Cache;

/// <summary>
/// In-process <see cref="IContentCache"/> backed by <see cref="IMemoryCache"/>.
/// Shared across all concurrent pipeline executions on the same host —
/// prevents 100 leads for the same property from each running a CMA or HomeSearch.
/// </summary>
internal sealed class MemoryContentCache(IMemoryCache cache) : IContentCache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1
        };
        cache.Set(key, value, options);
        return Task.CompletedTask;
    }
}
