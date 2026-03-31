using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Activities.Activation.PersistAgentProfile.Tests;

public class AgentProfilePersistActivityTests
{
    private readonly Mock<IFileStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly Mock<IAgentConfigService> _agentConfig = new(MockBehavior.Strict);
    private readonly AgentProfilePersistActivity _sut;
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string Handle = "test-agent";
    private static readonly CancellationToken Ct = CancellationToken.None;

    public AgentProfilePersistActivityTests()
    {
        _sut = new AgentProfilePersistActivity(
            _storage.Object,
            _agentConfig.Object,
            NullLogger<AgentProfilePersistActivity>.Instance);
    }

    private static ActivationOutputs MakeOutputs(
        string? voiceSkill = "Voice content",
        string? personalitySkill = "Personality content",
        byte[]? headshot = null,
        byte[]? logo = null)
    {
        return new ActivationOutputs
        {
            VoiceSkill = voiceSkill,
            PersonalitySkill = personalitySkill,
            CmaStyleGuide = "CMA content",
            MarketingStyle = "Marketing content",
            WebsiteStyleGuide = "Website content",
            SalesPipeline = "Pipeline content",
            CoachingReport = "Coaching content",
            HeadshotBytes = headshot,
            BrokerageLogoBytes = logo,
        };
    }

    private void SetupFolderCreation(string agentFolder)
    {
        _storage.Setup(s => s.EnsureFolderExistsAsync(agentFolder, Ct))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.EnsureFolderExistsAsync($"{agentFolder}/{AgentProfilePersistActivity.LeadsSubfolder}", Ct))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.EnsureFolderExistsAsync($"{agentFolder}/{AgentProfilePersistActivity.ConsentSubfolder}", Ct))
            .Returns(Task.CompletedTask);
    }

    private void SetupWriteOperations(string agentFolder)
    {
        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, It.IsAny<string>(), Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(agentFolder, It.IsAny<string>(), It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);
    }

    private void SetupAgentConfigService()
    {
        _agentConfig.Setup(a => a.GenerateAsync(
            AccountId, AgentId, Handle, It.IsAny<ActivationOutputs>(), Ct))
            .Returns(Task.CompletedTask);
    }

    // ── Folder creation ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CreatesAgentFolderAndLeadSubfolders()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);
        SetupWriteOperations(agentFolder);
        SetupAgentConfigService();

        await _sut.ExecuteAsync(AccountId, AgentId, Handle, MakeOutputs(), Ct);

        _storage.Verify(s => s.EnsureFolderExistsAsync(agentFolder, Ct), Times.Once);
        _storage.Verify(s => s.EnsureFolderExistsAsync(
            $"{agentFolder}/{AgentProfilePersistActivity.LeadsSubfolder}", Ct), Times.Once);
        _storage.Verify(s => s.EnsureFolderExistsAsync(
            $"{agentFolder}/{AgentProfilePersistActivity.ConsentSubfolder}", Ct), Times.Once);
    }

    // ── Markdown file writes ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WritesVoiceSkillFile()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);
        SetupWriteOperations(agentFolder);
        SetupAgentConfigService();

        await _sut.ExecuteAsync(AccountId, AgentId, Handle, MakeOutputs(), Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentProfilePersistActivity.VoiceSkillFile, "Voice content", Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WritesPersonalitySkillFile()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);
        SetupWriteOperations(agentFolder);
        SetupAgentConfigService();

        await _sut.ExecuteAsync(AccountId, AgentId, Handle, MakeOutputs(), Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentProfilePersistActivity.PersonalitySkillFile, "Personality content", Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NullMarkdownField_SkipsWrite()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);

        // All read calls return null (no existing files)
        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, It.IsAny<string>(), Ct))
            .ReturnsAsync((string?)null);

        // Allow writes for non-null files only
        _storage.Setup(s => s.WriteDocumentAsync(
            agentFolder, It.IsAny<string>(), It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        SetupAgentConfigService();

        // VoiceSkill is null — should be skipped
        var outputs = MakeOutputs(voiceSkill: null);
        await _sut.ExecuteAsync(AccountId, AgentId, Handle, outputs, Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentProfilePersistActivity.VoiceSkillFile, It.IsAny<string>(), Ct), Times.Never);
    }

    // ── Update existing files ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ExistingFile_UpdatesInsteadOfWrite()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);

        // Voice skill file already exists
        _storage.Setup(s => s.ReadDocumentAsync(
            agentFolder, AgentProfilePersistActivity.VoiceSkillFile, Ct))
            .ReturnsAsync("old content");
        _storage.Setup(s => s.UpdateDocumentAsync(
            agentFolder, AgentProfilePersistActivity.VoiceSkillFile, "Voice content", Ct))
            .Returns(Task.CompletedTask);

        // Other files don't exist
        _storage.Setup(s => s.ReadDocumentAsync(
            agentFolder,
            It.Is<string>(f => f != AgentProfilePersistActivity.VoiceSkillFile),
            Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            agentFolder,
            It.Is<string>(f => f != AgentProfilePersistActivity.VoiceSkillFile),
            It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        SetupAgentConfigService();

        await _sut.ExecuteAsync(AccountId, AgentId, Handle, MakeOutputs(), Ct);

        _storage.Verify(s => s.UpdateDocumentAsync(
            agentFolder, AgentProfilePersistActivity.VoiceSkillFile, "Voice content", Ct), Times.Once);
        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentProfilePersistActivity.VoiceSkillFile, It.IsAny<string>(), Ct), Times.Never);
    }

    // ── Binary assets ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithHeadshotBytes_WritesHeadshotAsBase64()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);

        var headshotBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        var expectedBase64 = Convert.ToBase64String(headshotBytes);

        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, It.IsAny<string>(), Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(agentFolder, It.IsAny<string>(), It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        SetupAgentConfigService();

        var outputs = MakeOutputs(headshot: headshotBytes);
        await _sut.ExecuteAsync(AccountId, AgentId, Handle, outputs, Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentProfilePersistActivity.HeadshotFile, expectedBase64, Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NullBinaryAsset_SkipsWrite()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);

        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, It.IsAny<string>(), Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(agentFolder, It.IsAny<string>(), It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        SetupAgentConfigService();

        // No headshot bytes
        var outputs = MakeOutputs(headshot: null, logo: null);
        await _sut.ExecuteAsync(AccountId, AgentId, Handle, outputs, Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentProfilePersistActivity.HeadshotFile, It.IsAny<string>(), Ct), Times.Never);
        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentProfilePersistActivity.BrokerageLogoFile, It.IsAny<string>(), Ct), Times.Never);
    }

    // ── AgentConfigService delegation ─────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_DelegatesConfigGenerationToAgentConfigService()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        SetupFolderCreation(agentFolder);
        SetupWriteOperations(agentFolder);
        SetupAgentConfigService();

        var outputs = MakeOutputs();
        await _sut.ExecuteAsync(AccountId, AgentId, Handle, outputs, Ct);

        _agentConfig.Verify(a => a.GenerateAsync(
            AccountId, AgentId, Handle, outputs, Ct), Times.Once);
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void FolderPrefix_IsCorrect()
    {
        Assert.Equal("real-estate-star", AgentProfilePersistActivity.FolderPrefix);
    }

    [Fact]
    public void LeadsSubfolder_IsCorrect()
    {
        Assert.Equal("leads", AgentProfilePersistActivity.LeadsSubfolder);
    }

    [Fact]
    public void ConsentSubfolder_IsCorrect()
    {
        Assert.Equal("leads/consent", AgentProfilePersistActivity.ConsentSubfolder);
    }

    // ── Drive sync verification ──────────────────────────────────────────────

    [Fact]
    public async Task VerifyDriveSyncAsync_LogsError_WhenProbeFileNotFound()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        var mockLogger = new Mock<ILogger<AgentProfilePersistActivity>>();
        var sut = new AgentProfilePersistActivity(
            _storage.Object,
            _agentConfig.Object,
            mockLogger.Object);

        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentProfilePersistActivity.VoiceSkillFile, Ct))
            .ReturnsAsync((string?)null);

        var outputs = MakeOutputs(voiceSkill: "Voice content");

        await sut.VerifyDriveSyncAsync(agentFolder, outputs, Ct);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("[PERSIST-AGENT-060]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyDriveSyncAsync_NoError_WhenProbeFileExists()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        var mockLogger = new Mock<ILogger<AgentProfilePersistActivity>>();
        var sut = new AgentProfilePersistActivity(
            _storage.Object,
            _agentConfig.Object,
            mockLogger.Object);

        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentProfilePersistActivity.VoiceSkillFile, Ct))
            .ReturnsAsync("Voice content");

        var outputs = MakeOutputs(voiceSkill: "Voice content");

        await sut.VerifyDriveSyncAsync(agentFolder, outputs, Ct);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifyDriveSyncAsync_FallsBackToPersonalityProbe_WhenVoiceIsNull()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        var mockLogger = new Mock<ILogger<AgentProfilePersistActivity>>();
        var sut = new AgentProfilePersistActivity(
            _storage.Object,
            _agentConfig.Object,
            mockLogger.Object);

        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentProfilePersistActivity.PersonalitySkillFile, Ct))
            .ReturnsAsync("Personality content");

        var outputs = MakeOutputs(voiceSkill: null, personalitySkill: "Personality content");

        await sut.VerifyDriveSyncAsync(agentFolder, outputs, Ct);

        _storage.Verify(s => s.ReadDocumentAsync(agentFolder, AgentProfilePersistActivity.PersonalitySkillFile, Ct), Times.Once);
    }

    [Fact]
    public async Task VerifyDriveSyncAsync_DoesNotThrow_WhenReadThrows()
    {
        var agentFolder = $"real-estate-star/{AgentId}";
        var mockLogger = new Mock<ILogger<AgentProfilePersistActivity>>();
        var sut = new AgentProfilePersistActivity(
            _storage.Object,
            _agentConfig.Object,
            mockLogger.Object);

        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentProfilePersistActivity.VoiceSkillFile, Ct))
            .ThrowsAsync(new InvalidOperationException("Drive unavailable"));

        var outputs = MakeOutputs(voiceSkill: "Voice content");

        var act = async () => await sut.VerifyDriveSyncAsync(agentFolder, outputs, Ct);

        await act.Should().NotThrowAsync();
    }
}
