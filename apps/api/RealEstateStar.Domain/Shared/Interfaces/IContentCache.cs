namespace RealEstateStar.Domain.Shared.Interfaces;

/// <summary>
/// Cross-lead content-addressed cache. Prevents re-running expensive activities
/// (CMA, HomeSearch) when 100 different leads submit for the same property.
/// Keys are SHA256 hashes of activity inputs computed by <see cref="ContentHash"/>.
/// </summary>
public interface IContentCache
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or null on cache miss.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> with the given TTL.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
}
