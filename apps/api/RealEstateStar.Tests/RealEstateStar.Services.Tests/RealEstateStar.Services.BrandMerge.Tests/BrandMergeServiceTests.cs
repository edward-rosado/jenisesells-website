using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Services.BrandMerge.Tests;

public class BrandMergeServiceTests
{
    private readonly Mock<IAnthropicClient> _anthropic = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly BrandMergeService _sut;
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private static readonly CancellationToken Ct = CancellationToken.None;

    public BrandMergeServiceTests()
    {
        _sut = new BrandMergeService(
            _anthropic.Object,
            _storage.Object,
            NullLogger<BrandMergeService>.Instance);
    }

    private static AnthropicResponse MakeResponse(string content) =>
        new(content, 100, 200, 500);

    private void SetupExistingBrandFiles(string? profile, string? voice)
    {
        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AccountId}", BrandMergeService.BrandProfileFile, Ct))
            .ReturnsAsync(profile);
        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AccountId}", BrandMergeService.BrandVoiceFile, Ct))
            .ReturnsAsync(voice);
    }

    // ── First agent (no existing brand) ───────────────────────────────────────

    [Fact]
    public async Task MergeAsync_FirstAgent_NoBrandFiles_CallsAnthropicAndReturnsBoth()
    {
        SetupExistingBrandFiles(null, null);

        const string claudeResponse = "# Brand Profile\nStrong brand.\n---BRAND-VOICE---\n# Brand Voice\nFriendly tone.";
        _anthropic.Setup(a => a.SendAsync(
            BrandMergeService.Model,
            It.IsAny<string>(),
            It.IsAny<string>(),
            BrandMergeService.MaxTokens,
            BrandMergeService.Pipeline,
            Ct))
            .ReturnsAsync(MakeResponse(claudeResponse));

        var result = await _sut.MergeAsync(AccountId, AgentId, "Branding Kit data", "Voice data", Ct);

        Assert.Equal("# Brand Profile\nStrong brand.", result.BrandProfileMarkdown);
        Assert.Equal("# Brand Voice\nFriendly tone.", result.BrandVoiceMarkdown);
    }

    [Fact]
    public async Task MergeAsync_FirstAgent_AnthropicCalledWithCreationPrompt()
    {
        SetupExistingBrandFiles(null, null);

        _anthropic.Setup(a => a.SendAsync(
            BrandMergeService.Model,
            It.Is<string>(s => s.Contains("brand strategist for real estate")),
            It.Is<string>(u => u.Contains("Create a Brand Profile")),
            BrandMergeService.MaxTokens,
            BrandMergeService.Pipeline,
            Ct))
            .ReturnsAsync(MakeResponse("Profile\n---BRAND-VOICE---\nVoice"));

        var result = await _sut.MergeAsync(AccountId, AgentId, "branding", "voice", Ct);

        Assert.NotNull(result);
        _anthropic.Verify(a => a.SendAsync(
            BrandMergeService.Model,
            It.IsAny<string>(),
            It.Is<string>(u => u.Contains("Create a Brand Profile")),
            BrandMergeService.MaxTokens,
            BrandMergeService.Pipeline,
            Ct), Times.Once);
    }

    // ── Subsequent agent (existing brand) ─────────────────────────────────────

    [Fact]
    public async Task MergeAsync_SubsequentAgent_ExistingBrandFiles_CallsEnrichPrompt()
    {
        SetupExistingBrandFiles("# Existing Profile", "# Existing Voice");

        _anthropic.Setup(a => a.SendAsync(
            BrandMergeService.Model,
            It.IsAny<string>(),
            It.Is<string>(u => u.Contains("Enrich the brokerage brand")),
            BrandMergeService.MaxTokens,
            BrandMergeService.Pipeline,
            Ct))
            .ReturnsAsync(MakeResponse("Enriched Profile\n---BRAND-VOICE---\nEnriched Voice"));

        var result = await _sut.MergeAsync(AccountId, AgentId, "New branding", "New voice", Ct);

        Assert.Contains("Enriched Profile", result.BrandProfileMarkdown);
        Assert.Contains("Enriched Voice", result.BrandVoiceMarkdown);
    }

    [Fact]
    public async Task MergeAsync_SubsequentAgent_ExistingDataPassedToClaude()
    {
        const string existingProfile = "# Existing Brand Profile\nNavy blue palette.";
        const string existingVoice = "# Existing Brand Voice\nProfessional tone.";
        SetupExistingBrandFiles(existingProfile, existingVoice);

        string? capturedMessage = null;
        _anthropic.Setup(a => a.SendAsync(
            BrandMergeService.Model,
            It.IsAny<string>(),
            It.IsAny<string>(),
            BrandMergeService.MaxTokens,
            BrandMergeService.Pipeline,
            Ct))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedMessage = userMsg)
            .ReturnsAsync(MakeResponse("New Profile\n---BRAND-VOICE---\nNew Voice"));

        await _sut.MergeAsync(AccountId, AgentId, "new branding", "new voice", Ct);

        Assert.NotNull(capturedMessage);
        Assert.Contains("Navy blue palette", capturedMessage);
        Assert.Contains("Professional tone", capturedMessage);
    }

    // ── Response parsing ──────────────────────────────────────────────────────

    [Fact]
    public void ParseBrandResponse_WithSeparator_SplitsCorrectly()
    {
        const string content = "# Brand Profile\nVisual identity.\n---BRAND-VOICE---\n# Brand Voice\nFriendly.";

        var (profile, voice) = BrandMergeService.ParseBrandResponse(content, AgentId);

        Assert.Equal("# Brand Profile\nVisual identity.", profile);
        Assert.Equal("# Brand Voice\nFriendly.", voice);
    }

    [Fact]
    public void ParseBrandResponse_WithoutSeparator_TreatsAllAsProfile()
    {
        const string content = "Just a profile, no separator.";

        var (profile, voice) = BrandMergeService.ParseBrandResponse(content, AgentId);

        Assert.Equal(content, profile);
        Assert.Contains(AgentId, voice);
    }

    [Fact]
    public void ParseBrandResponse_TrimsWhitespace()
    {
        const string content = "  Profile Content  \n---BRAND-VOICE---\n  Voice Content  ";

        var (profile, voice) = BrandMergeService.ParseBrandResponse(content, AgentId);

        Assert.Equal("Profile Content", profile);
        Assert.Equal("Voice Content", voice);
    }

    [Fact]
    public void FolderPrefix_IsCorrect()
    {
        Assert.Equal("real-estate-star", BrandMergeService.FolderPrefix);
    }

    [Fact]
    public void BrandProfileFile_IsCorrect()
    {
        Assert.Equal("Brand Profile.md", BrandMergeService.BrandProfileFile);
    }

    [Fact]
    public void BrandVoiceFile_IsCorrect()
    {
        Assert.Equal("Brand Voice.md", BrandMergeService.BrandVoiceFile);
    }
}
