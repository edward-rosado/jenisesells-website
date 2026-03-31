using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Services.BrandMerge;

/// <summary>
/// Claude-powered merge of brand signals across agents in a brokerage.
/// For the first agent, creates a new brand profile from their signals.
/// For subsequent agents, enriches the existing profile with new patterns.
/// Per-account (brokerage) brand files live at real-estate-star/{accountId}/.
/// </summary>
public sealed class BrandMergeService(
    IAnthropicClient anthropic,
    IFileStorageProvider storage,
    ILogger<BrandMergeService> logger) : IBrandMergeService
{
    internal const string FolderPrefix = "real-estate-star";
    internal const string BrandProfileFile = "Brand Profile.md";
    internal const string BrandVoiceFile = "Brand Voice.md";
    internal const string Model = "claude-sonnet-4-6";
    internal const string Pipeline = "brand-merge";
    internal const int MaxTokens = 2000;

    public async Task<BrandMergeResult> MergeAsync(
        string accountId,
        string agentId,
        string newBrandingKit,
        string newVoiceSkill,
        CancellationToken ct)
    {
        var accountFolder = $"{FolderPrefix}/{accountId}";

        // Load existing brand files (null = first agent)
        var existingProfileTask = storage.ReadDocumentAsync(accountFolder, BrandProfileFile, ct);
        var existingVoiceTask = storage.ReadDocumentAsync(accountFolder, BrandVoiceFile, ct);
        await Task.WhenAll(existingProfileTask, existingVoiceTask);

        var existingProfile = await existingProfileTask;
        var existingVoice = await existingVoiceTask;

        var isFirstAgent = existingProfile is null && existingVoice is null;

        logger.LogInformation(
            "[BRAND-010] Merging brand for accountId={AccountId}, agentId={AgentId}, isFirstAgent={IsFirstAgent}",
            accountId, agentId, isFirstAgent);

        string brandProfileMarkdown;
        string brandVoiceMarkdown;

        if (isFirstAgent)
        {
            (brandProfileMarkdown, brandVoiceMarkdown) = await CreateInitialBrandAsync(
                accountId, agentId, newBrandingKit, newVoiceSkill, ct);
        }
        else
        {
            (brandProfileMarkdown, brandVoiceMarkdown) = await EnrichBrandAsync(
                accountId, agentId, existingProfile!, existingVoice!, newBrandingKit, newVoiceSkill, ct);
        }

        logger.LogInformation(
            "[BRAND-090] Brand merge complete for accountId={AccountId}", accountId);

        return new BrandMergeResult(brandProfileMarkdown, brandVoiceMarkdown);
    }

    // ── Initial brand creation (first agent) ──────────────────────────────────

    private async Task<(string profile, string voice)> CreateInitialBrandAsync(
        string accountId,
        string agentId,
        string brandingKit,
        string voiceSkill,
        CancellationToken ct)
    {
        const string systemPrompt =
            "You are a brand strategist for real estate professionals. " +
            "Analyze the agent's branding and voice data to produce a professional brand profile " +
            "and brand voice document. Both must be in Markdown. " +
            "Brand Profile should cover: visual identity, positioning, market segment, differentiators. " +
            "Brand Voice should cover: tone, communication style, key phrases, catchphrase, sign-off.";

        var userMessage =
            $"Create a Brand Profile and Brand Voice for this real estate agent.\n\n" +
            $"## Branding Kit\n{brandingKit}\n\n" +
            $"## Voice Skill\n{voiceSkill}\n\n" +
            "Output two sections separated by '---BRAND-VOICE---':\n" +
            "First the Brand Profile markdown, then the Brand Voice markdown.";

        var response = await anthropic.SendAsync(
            Model, systemPrompt, userMessage, MaxTokens, Pipeline, ct);

        return ParseBrandResponse(response.Content, agentId);
    }

    // ── Brand enrichment (subsequent agents) ──────────────────────────────────

    private async Task<(string profile, string voice)> EnrichBrandAsync(
        string accountId,
        string agentId,
        string existingProfile,
        string existingVoice,
        string newBrandingKit,
        string newVoiceSkill,
        CancellationToken ct)
    {
        const string systemPrompt =
            "You are a brand strategist for a real estate brokerage. " +
            "Enrich the existing Brand Profile and Brand Voice with new agent signals. " +
            "Synthesize recurring patterns, expand the voice range, update the visual palette if needed. " +
            "Both documents must remain in Markdown. " +
            "Preserve existing patterns while integrating what is distinctive about the new agent.";

        var userMessage =
            $"Enrich the brokerage brand with a new agent's signals.\n\n" +
            $"## Existing Brand Profile\n{existingProfile}\n\n" +
            $"## Existing Brand Voice\n{existingVoice}\n\n" +
            $"## New Agent Branding Kit\n{newBrandingKit}\n\n" +
            $"## New Agent Voice Skill\n{newVoiceSkill}\n\n" +
            "Output two sections separated by '---BRAND-VOICE---':\n" +
            "First the enriched Brand Profile markdown, then the enriched Brand Voice markdown.";

        var response = await anthropic.SendAsync(
            Model, systemPrompt, userMessage, MaxTokens, Pipeline, ct);

        return ParseBrandResponse(response.Content, agentId);
    }

    // ── Response parsing ──────────────────────────────────────────────────────

    internal static (string profile, string voice) ParseBrandResponse(string content, string agentId)
    {
        const string separator = "---BRAND-VOICE---";
        var idx = content.IndexOf(separator, StringComparison.Ordinal);

        if (idx < 0)
        {
            // Fallback: treat entire response as profile, use a simple voice placeholder
            return (content.Trim(), $"# Brand Voice\n\n_Extracted from agent {agentId} activation._");
        }

        var profile = content[..idx].Trim();
        var voice = content[(idx + separator.Length)..].Trim();
        return (profile, voice);
    }
}
