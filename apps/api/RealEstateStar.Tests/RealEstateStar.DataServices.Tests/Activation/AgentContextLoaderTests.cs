using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.DataServices.Activation;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Tests.Activation;

public class AgentContextLoaderTests
{
    private readonly Mock<IFileStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly AgentContextLoader _sut;
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private static readonly CancellationToken Ct = CancellationToken.None;

    public AgentContextLoaderTests()
    {
        _sut = new AgentContextLoader(_storage.Object, NullLogger<AgentContextLoader>.Instance);
    }

    private void SetupAgentFile(string fileName, string? content) =>
        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AgentId}", fileName, Ct))
            .ReturnsAsync(content);

    private void SetupAllAgentFiles(
        string? voice = "Voice content",
        string? personality = "Personality content",
        string? cma = "CMA content",
        string? website = "Website content",
        string? pipeline = "Pipeline content",
        string? coaching = "Coaching content",
        string? branding = "Branding content",
        string? compliance = "Compliance content",
        string? pipelineJson = null)
    {
        SetupAgentFile(AgentContextLoader.VoiceSkillFile, voice);
        SetupAgentFile(AgentContextLoader.PersonalitySkillFile, personality);
        SetupAgentFile(AgentContextLoader.CmaStyleGuideFile, cma);
        SetupAgentFile(AgentContextLoader.WebsiteStyleGuideFile, website);
        SetupAgentFile(AgentContextLoader.SalesPipelineFile, pipeline);
        SetupAgentFile(AgentContextLoader.CoachingReportFile, coaching);
        SetupAgentFile(AgentContextLoader.BrandingKitFile, branding);
        SetupAgentFile(AgentContextLoader.ComplianceAnalysisFile, compliance);
        SetupAgentFile(AgentContextLoader.PipelineJsonFile, pipelineJson);
    }

    [Fact]
    public async Task LoadAsync_AllFilesPresent_ReturnsFullyPopulatedContext_WithIsActivatedTrue()
    {
        SetupAllAgentFiles();

        var result = await _sut.LoadAsync(AccountId, AgentId, Ct);

        Assert.NotNull(result);
        Assert.True(result.IsActivated);
        Assert.False(result.IsLowConfidence);
        Assert.Equal("Voice content", result.VoiceSkill);
        Assert.Equal("Personality content", result.PersonalitySkill);
        Assert.Equal("CMA content", result.CmaStyleGuide);
        Assert.Equal("Website content", result.WebsiteStyleGuide);
        Assert.Equal("Pipeline content", result.SalesPipeline);
        Assert.Equal("Coaching content", result.CoachingReport);
        Assert.Equal("Branding content", result.BrandingKit);
        Assert.Equal("Compliance content", result.ComplianceAnalysis);
    }

    [Fact]
    public async Task LoadAsync_NoFilesExist_ReturnsNull()
    {
        SetupAllAgentFiles(null, null, null, null, null, null, null, null);

        var result = await _sut.LoadAsync(AccountId, AgentId, Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_PartialFiles_ReturnsContextWithNullsForMissing_IsActivatedFalse()
    {
        // Only voice and personality present — missing cma, pipeline, coaching
        SetupAllAgentFiles(
            voice: "Voice content",
            personality: "Personality content",
            cma: null,
            website: null,
            pipeline: null,
            coaching: null,
            branding: null,
            compliance: null);

        var result = await _sut.LoadAsync(AccountId, AgentId, Ct);

        Assert.NotNull(result);
        Assert.False(result.IsActivated);
        Assert.Equal("Voice content", result.VoiceSkill);
        Assert.Equal("Personality content", result.PersonalitySkill);
        Assert.Null(result.CmaStyleGuide);
        Assert.Null(result.SalesPipeline);
        Assert.Null(result.CoachingReport);
    }

    [Fact]
    public async Task LoadAsync_FileContainsLowConfidenceMarker_SetsIsLowConfidenceTrue()
    {
        SetupAllAgentFiles(
            voice: "This is a Low confidence analysis due to insufficient data.");

        var result = await _sut.LoadAsync(AccountId, AgentId, Ct);

        Assert.NotNull(result);
        Assert.True(result.IsLowConfidence);
    }

    [Fact]
    public async Task LoadAsync_PerAgentFolderUsesAgentId()
    {
        SetupAllAgentFiles();

        await _sut.LoadAsync(AccountId, AgentId, Ct);

        _storage.Verify(s => s.ReadDocumentAsync(
            $"real-estate-star/{AgentId}", AgentContextLoader.VoiceSkillFile, Ct), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_WhenAccountIdEqualsAgentId_StillLoadsFromSameFolder()
    {
        // Single-agent case: accountId == agentId, both read from same folder
        const string singleId = "jenise-buckalew";

        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{singleId}", It.IsAny<string>(), Ct))
            .ReturnsAsync("content");

        var result = await _sut.LoadAsync(singleId, singleId, Ct);

        Assert.NotNull(result);
        Assert.True(result.IsActivated);
    }

    [Fact]
    public void FolderPrefix_IsCorrect()
    {
        Assert.Equal("real-estate-star", AgentContextLoader.FolderPrefix);
    }

    [Fact]
    public void LowConfidenceMarker_IsCorrect()
    {
        Assert.Equal("Low confidence", AgentContextLoader.LowConfidenceMarker);
    }
}
