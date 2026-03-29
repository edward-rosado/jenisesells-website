using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Activities.Activation.BrandMerge.Tests;

public class BrandMergeActivityTests
{
    private readonly Mock<IBrandMergeService> _brandMergeService = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly BrandMergeActivity _sut;
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private static readonly CancellationToken Ct = CancellationToken.None;

    public BrandMergeActivityTests()
    {
        _sut = new BrandMergeActivity(
            _brandMergeService.Object,
            _storage.Object,
            NullLogger<BrandMergeActivity>.Instance);
    }

    private static BrandMergeResult MakeResult(
        string profile = "# Brand Profile\nContent.",
        string voice = "# Brand Voice\nFriendly.")
    {
        return new BrandMergeResult(profile, voice);
    }

    private void SetupMergeService(BrandMergeResult result)
    {
        _brandMergeService.Setup(b => b.MergeAsync(
            AccountId, AgentId, It.IsAny<string>(), It.IsAny<string>(), Ct))
            .ReturnsAsync(result);
    }

    private void SetupAccountFolder()
    {
        _storage.Setup(s => s.EnsureFolderExistsAsync(
            $"real-estate-star/{AccountId}", Ct))
            .Returns(Task.CompletedTask);
    }

    private void SetupWriteOperations(string? existingProfile = null, string? existingVoice = null)
    {
        var accountFolder = $"real-estate-star/{AccountId}";

        _storage.Setup(s => s.ReadDocumentAsync(
            accountFolder, BrandMergeActivity.BrandProfileFile, Ct))
            .ReturnsAsync(existingProfile);

        _storage.Setup(s => s.ReadDocumentAsync(
            accountFolder, BrandMergeActivity.BrandVoiceFile, Ct))
            .ReturnsAsync(existingVoice);

        if (existingProfile is null)
            _storage.Setup(s => s.WriteDocumentAsync(
                accountFolder, BrandMergeActivity.BrandProfileFile, It.IsAny<string>(), Ct))
                .Returns(Task.CompletedTask);
        else
            _storage.Setup(s => s.UpdateDocumentAsync(
                accountFolder, BrandMergeActivity.BrandProfileFile, It.IsAny<string>(), Ct))
                .Returns(Task.CompletedTask);

        if (existingVoice is null)
            _storage.Setup(s => s.WriteDocumentAsync(
                accountFolder, BrandMergeActivity.BrandVoiceFile, It.IsAny<string>(), Ct))
                .Returns(Task.CompletedTask);
        else
            _storage.Setup(s => s.UpdateDocumentAsync(
                accountFolder, BrandMergeActivity.BrandVoiceFile, It.IsAny<string>(), Ct))
                .Returns(Task.CompletedTask);
    }

    // ── Execution flow ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CallsBrandMergeService()
    {
        SetupAccountFolder();
        SetupMergeService(MakeResult());
        SetupWriteOperations();

        await _sut.ExecuteAsync(AccountId, AgentId, "branding kit", "voice skill", Ct);

        _brandMergeService.Verify(b => b.MergeAsync(
            AccountId, AgentId, "branding kit", "voice skill", Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WritesProfileAndVoiceToAccountFolder()
    {
        var accountFolder = $"real-estate-star/{AccountId}";
        SetupAccountFolder();
        SetupMergeService(MakeResult("My Profile", "My Voice"));
        SetupWriteOperations();

        await _sut.ExecuteAsync(AccountId, AgentId, "branding", "voice", Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            accountFolder, BrandMergeActivity.BrandProfileFile, "My Profile", Ct), Times.Once);
        _storage.Verify(s => s.WriteDocumentAsync(
            accountFolder, BrandMergeActivity.BrandVoiceFile, "My Voice", Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingBrandFiles_UpdatesInsteadOfWrite()
    {
        var accountFolder = $"real-estate-star/{AccountId}";
        SetupAccountFolder();
        SetupMergeService(MakeResult("Updated Profile", "Updated Voice"));
        SetupWriteOperations(existingProfile: "old profile", existingVoice: "old voice");

        await _sut.ExecuteAsync(AccountId, AgentId, "new branding", "new voice", Ct);

        _storage.Verify(s => s.UpdateDocumentAsync(
            accountFolder, BrandMergeActivity.BrandProfileFile, "Updated Profile", Ct), Times.Once);
        _storage.Verify(s => s.UpdateDocumentAsync(
            accountFolder, BrandMergeActivity.BrandVoiceFile, "Updated Voice", Ct), Times.Once);
        _storage.Verify(s => s.WriteDocumentAsync(
            accountFolder, BrandMergeActivity.BrandProfileFile, It.IsAny<string>(), Ct), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_EnsuresAccountFolderExists()
    {
        SetupAccountFolder();
        SetupMergeService(MakeResult());
        SetupWriteOperations();

        await _sut.ExecuteAsync(AccountId, AgentId, "branding", "voice", Ct);

        _storage.Verify(s => s.EnsureFolderExistsAsync(
            $"real-estate-star/{AccountId}", Ct), Times.Once);
    }

    // ── Single writer guarantee ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WritesToAccountFolder_NotAgentFolder()
    {
        // Brand files MUST go to accountId folder, never agentId folder
        SetupAccountFolder();
        SetupMergeService(MakeResult());
        SetupWriteOperations();

        await _sut.ExecuteAsync(AccountId, AgentId, "branding", "voice", Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            It.Is<string>(f => f.Contains(AgentId) && !f.Contains(AccountId)),
            It.IsAny<string>(), It.IsAny<string>(), Ct), Times.Never);
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void FolderPrefix_IsCorrect()
    {
        Assert.Equal("real-estate-star", BrandMergeActivity.FolderPrefix);
    }

    [Fact]
    public void BrandProfileFile_IsCorrect()
    {
        Assert.Equal("Brand Profile.md", BrandMergeActivity.BrandProfileFile);
    }

    [Fact]
    public void BrandVoiceFile_IsCorrect()
    {
        Assert.Equal("Brand Voice.md", BrandMergeActivity.BrandVoiceFile);
    }
}
