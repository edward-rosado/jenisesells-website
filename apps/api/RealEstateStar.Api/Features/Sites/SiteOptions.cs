namespace RealEstateStar.Api.Features.Sites;

/// <summary>
/// Configuration for site management endpoints.
/// Bound from the "SiteContent" configuration section (shared with Functions).
/// </summary>
public sealed class SiteOptions
{
    /// <summary>
    /// Cloudflare KV namespace ID for site content and state storage.
    /// Key patterns: site-state:v1:{accountId}, content:v1:{accountId}:{locale}:draft
    /// </summary>
    public string KvNamespaceId { get; init; } = "";
}
