namespace RealEstateStar.Functions.Activation;

/// <summary>
/// Configuration for site content persistence (B10).
/// Bound from the "SiteContent" configuration section.
/// </summary>
public sealed class SiteContentOptions
{
    /// <summary>
    /// Cloudflare KV namespace ID for site content storage.
    /// Key pattern: content:v1:{accountId}:{locale}:draft
    /// </summary>
    public string KvNamespaceId { get; init; } = "";
}
