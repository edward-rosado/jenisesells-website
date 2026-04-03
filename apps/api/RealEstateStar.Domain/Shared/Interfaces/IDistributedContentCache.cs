namespace RealEstateStar.Domain.Shared.Interfaces;

/// <summary>
/// Distributed content cache backed by persistent storage (e.g., Azure Table Storage).
/// Cross-function dedup for CMA and HomeSearch results.
/// Same contract as <see cref="IContentCache"/> but survives across process restarts.
/// </summary>
public interface IDistributedContentCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
    Task RemoveAsync(string key, CancellationToken ct);
}
