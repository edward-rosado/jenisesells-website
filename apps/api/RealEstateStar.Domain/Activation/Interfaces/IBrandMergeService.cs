namespace RealEstateStar.Domain.Activation.Interfaces;

public interface IBrandMergeService
{
    /// <summary>
    /// Merges new brand signals with existing Brand Profile and Brand Voice.
    /// For the first agent in an account, creates a new profile (no merge needed).
    /// Returns the merged Brand Profile markdown and Brand Voice markdown.
    /// </summary>
    Task<BrandMergeResult> MergeAsync(
        string accountId,
        string agentId,
        string newBrandingKit,
        string newVoiceSkill,
        CancellationToken ct,
        string? locale = null);
}

public sealed record BrandMergeResult(
    string BrandProfileMarkdown,
    string BrandVoiceMarkdown);
