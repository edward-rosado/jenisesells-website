namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Result of BuildLocalizedSiteContent activity.
/// Full = all fields generated successfully.
/// Fallback = minimal content from account.json + template defaults.
/// NeedsInfo = missing required compliance fields, cannot publish.
/// </summary>
public sealed record BuildResult(
    BuildResultType ResultType,
    IReadOnlyDictionary<string, object> ContentByLocale,
    string? FallbackReason);

public enum BuildResultType
{
    Full,
    Fallback,
    NeedsInfo
}
