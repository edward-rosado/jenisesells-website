namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Result of the Phase 2.25 synthesis merge — cross-references outputs from all Phase 2 workers.
/// </summary>
public sealed record SynthesisMergeResult(
    string? EnrichedCoachingReport,
    IReadOnlyList<Contradiction> Contradictions,
    string? StrengthsSummary);

/// <summary>
/// A detected mismatch between two worker outputs.
/// </summary>
public sealed record Contradiction(
    string Source1,
    string Source2,
    string Signal,
    string Description);
