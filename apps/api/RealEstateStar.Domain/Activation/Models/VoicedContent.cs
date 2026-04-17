namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Request to generate a single voiced field via VoicedContentGenerator.
/// </summary>
public sealed record VoicedRequest<T>(
    SiteFacts Facts,
    string Locale,
    LocaleVoice Voice,
    FieldSpec<T> Field,
    string PipelineStep);

/// <summary>
/// Batched request to generate multiple voiced fields in a single Claude call.
/// </summary>
public sealed record VoicedBatchRequest<T>(
    SiteFacts Facts,
    string Locale,
    LocaleVoice Voice,
    IReadOnlyList<FieldSpec<T>> Fields,
    string PipelineStep);

/// <summary>
/// Result of a voiced content generation attempt.
/// </summary>
public sealed record VoicedResult<T>(
    T Value,
    bool IsFallback,
    string? FailureReason,
    ClaudeCallMetrics Metrics);

/// <summary>
/// Token usage and cost metrics from a single Claude call.
/// </summary>
public sealed record ClaudeCallMetrics(
    int InputTokens,
    int OutputTokens,
    double EstimatedCostUsd,
    double DurationMs);
