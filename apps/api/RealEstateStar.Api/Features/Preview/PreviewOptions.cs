namespace RealEstateStar.Api.Features.Preview;

/// <summary>
/// Configuration for preview session functionality.
/// Bound from the "Preview" configuration section.
/// </summary>
public sealed class PreviewOptions
{
    /// <summary>
    /// HMAC-SHA256 key used to sign and verify exchange tokens.
    /// Must be at least 32 characters. Required in production.
    /// </summary>
    public string HmacKey { get; init; } = "";
}
