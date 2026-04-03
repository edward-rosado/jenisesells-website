using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Services.WelcomeNotification.Tests;

public class WelcomeNotificationServiceTests
{
    private readonly Mock<IWhatsAppSender> _whatsApp = new(MockBehavior.Strict);
    private readonly Mock<IGmailSender> _gmail = new(MockBehavior.Strict);
    private readonly Mock<IAnthropicClient> _anthropic = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly WelcomeNotificationService _sut;
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string Handle = "test-agent";
    private static readonly CancellationToken Ct = CancellationToken.None;

    public WelcomeNotificationServiceTests()
    {
        _sut = new WelcomeNotificationService(
            _whatsApp.Object,
            _gmail.Object,
            _anthropic.Object,
            _storage.Object,
            new NullIdempotencyStore(),
            NullLogger<WelcomeNotificationService>.Instance);
    }

    private static AnthropicResponse MakeResponse(string content) =>
        new(content, 100, 50, 300);

    private static ActivationOutputs MakeOutputs(
        string? phone = "(555) 123-4567",
        string? email = "agent@example.com",
        string? name = "Jane Smith",
        bool whatsAppEnabled = false)
    {
        return new ActivationOutputs
        {
            AgentPhone = phone,
            AgentEmail = email,
            AgentName = name,
            VoiceSkill = "Voice skill content",
            PersonalitySkill = "Personality content",
            SalesPipeline = "Pipeline content",
            CoachingReport = "Coaching content",
            Discovery = new AgentDiscovery(
                null, null, phone,
                [], [], [],
                null, whatsAppEnabled),
        };
    }

    private void SetupNoExistingSentFile()
    {
        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, Ct))
            .ReturnsAsync((string?)null);
    }

    private void SetupSentFlag()
    {
        _storage.Setup(s => s.WriteDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);
    }

    private void SetupAnthropicDraft(string message = "Welcome! You're live.")
    {
        _anthropic.Setup(a => a.SendAsync(
            WelcomeNotificationService.Model,
            It.IsAny<string>(),
            It.IsAny<string>(),
            WelcomeNotificationService.MaxTokens,
            WelcomeNotificationService.Pipeline,
            Ct))
            .ReturnsAsync(MakeResponse(message));
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_AlreadySent_DoesNotSendAgain()
    {
        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, Ct))
            .ReturnsAsync("---\nsent: true\n---\nWelcome message.");

        await _sut.SendAsync(AccountId, AgentId, Handle, MakeOutputs(), Ct);

        _anthropic.VerifyNoOtherCalls();
        _whatsApp.VerifyNoOtherCalls();
        _gmail.VerifyNoOtherCalls();
    }

    // ── WhatsApp path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhatsAppEnabled_SendsViaWhatsApp()
    {
        var outputs = MakeOutputs(whatsAppEnabled: true);
        SetupNoExistingSentFile();
        SetupAnthropicDraft("Welcome via WhatsApp!");
        SetupSentFlag();

        _whatsApp.Setup(w => w.SendFreeformAsync("(555) 123-4567", "Welcome via WhatsApp!", Ct))
            .ReturnsAsync("msg-id-123");

        await _sut.SendAsync(AccountId, AgentId, Handle, outputs, Ct);

        _whatsApp.Verify(w => w.SendFreeformAsync("(555) 123-4567", It.IsAny<string>(), Ct), Times.Once);
        _gmail.VerifyNoOtherCalls();
    }

    // ── Email fallback ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhatsAppDisabled_FallsBackToEmail()
    {
        var outputs = MakeOutputs(whatsAppEnabled: false);
        SetupNoExistingSentFile();
        SetupAnthropicDraft("Welcome via email!");
        SetupSentFlag();

        _gmail.Setup(g => g.SendAsync(
            AccountId, AgentId, "agent@example.com",
            "Welcome to Real Estate Star!", It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        await _sut.SendAsync(AccountId, AgentId, Handle, outputs, Ct);

        _gmail.Verify(g => g.SendAsync(
            AccountId, AgentId, "agent@example.com",
            "Welcome to Real Estate Star!", It.IsAny<string>(), Ct), Times.Once);
        _whatsApp.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_WhatsAppEnabledButFails_FallsBackToEmail()
    {
        var outputs = MakeOutputs(whatsAppEnabled: true);
        SetupNoExistingSentFile();
        SetupAnthropicDraft("Welcome!");
        SetupSentFlag();

        _whatsApp.Setup(w => w.SendFreeformAsync("(555) 123-4567", It.IsAny<string>(), Ct))
            .ThrowsAsync(new WhatsAppApiException(100, "connection error"));

        _gmail.Setup(g => g.SendAsync(
            AccountId, AgentId, "agent@example.com",
            "Welcome to Real Estate Star!", It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        await _sut.SendAsync(AccountId, AgentId, Handle, outputs, Ct);

        _gmail.Verify(g => g.SendAsync(
            AccountId, AgentId, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), Ct), Times.Once);
    }

    // ── Sent flag persistence ─────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_AfterSuccessfulSend_WritesSentFlag()
    {
        var outputs = MakeOutputs(whatsAppEnabled: false);
        SetupNoExistingSentFile();
        SetupAnthropicDraft("Welcome!");

        string? writtenContent = null;
        _gmail.Setup(g => g.SendAsync(
            AccountId, AgentId, "agent@example.com",
            "Welcome to Real Estate Star!", It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        _storage.Setup(s => s.WriteDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, It.IsAny<string>(), Ct))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) => writtenContent = content)
            .Returns(Task.CompletedTask);

        await _sut.SendAsync(AccountId, AgentId, Handle, outputs, Ct);

        Assert.NotNull(writtenContent);
        Assert.Contains("sent: true", writtenContent);
    }

    // ── URL construction ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_SingleAgent_IncludesSingleAgentUrl()
    {
        const string singleHandle = "jane-smith";
        var outputs = MakeOutputs(whatsAppEnabled: false);

        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, Ct))
            .ReturnsAsync((string?)null);

        string? capturedUser = null;
        _anthropic.Setup(a => a.SendAsync(
            WelcomeNotificationService.Model,
            It.IsAny<string>(),
            It.IsAny<string>(),
            WelcomeNotificationService.MaxTokens,
            WelcomeNotificationService.Pipeline,
            Ct))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, user, _, _, _) => capturedUser = user)
            .ReturnsAsync(MakeResponse("Welcome!"));

        _gmail.Setup(g => g.SendAsync(
            AccountId, AgentId, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        _storage.Setup(s => s.WriteDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        // Single agent: accountId == agentId
        await _sut.SendAsync(AgentId, AgentId, singleHandle, outputs, Ct);

        Assert.NotNull(capturedUser);
        Assert.Contains($"https://{singleHandle}.real-estate-star.com", capturedUser);
    }

    [Fact]
    public async Task SendAsync_BrokerageAgent_IncludesBrokerageAgentUrl()
    {
        var outputs = MakeOutputs(whatsAppEnabled: false);

        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, Ct))
            .ReturnsAsync((string?)null);

        string? capturedUser = null;
        _anthropic.Setup(a => a.SendAsync(
            WelcomeNotificationService.Model,
            It.IsAny<string>(),
            It.IsAny<string>(),
            WelcomeNotificationService.MaxTokens,
            WelcomeNotificationService.Pipeline,
            Ct))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, user, _, _, _) => capturedUser = user)
            .ReturnsAsync(MakeResponse("Welcome!"));

        _gmail.Setup(g => g.SendAsync(
            AccountId, AgentId, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        _storage.Setup(s => s.WriteDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        // Brokerage agent: accountId != agentId
        await _sut.SendAsync(AccountId, AgentId, Handle, outputs, Ct);

        Assert.NotNull(capturedUser);
        Assert.Contains($"https://{AccountId}.real-estate-star.com/agents/{AgentId}", capturedUser);
    }

    // ── HTML wrapper ──────────────────────────────────────────────────────────

    [Fact]
    public void WrapHtml_EncodesSpecialCharacters()
    {
        var html = WelcomeNotificationService.WrapHtml("<script>alert(1)</script>", "Agent Name");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void WrapHtml_IncludesAgentName()
    {
        var html = WelcomeNotificationService.WrapHtml("Welcome!", "Jenise Buckalew");

        Assert.Contains("Jenise Buckalew", html);
    }

    [Fact]
    public void WrapHtml_NullAgentName_StillRendersHeader()
    {
        var html = WelcomeNotificationService.WrapHtml("Welcome!", null);

        Assert.Contains("Welcome to Real Estate Star", html);
    }

    [Fact]
    public void WelcomeSentFile_IsCorrect()
    {
        Assert.Equal("Welcome Sent.md", WelcomeNotificationService.WelcomeSentFile);
    }

    // ── Idempotency store guard ───────────────────────────────────────────────

    private WelcomeNotificationService CreateSutWithStore(InMemoryIdempotencyStore store) =>
        new(_whatsApp.Object, _gmail.Object, _anthropic.Object, _storage.Object, store,
            NullLogger<WelcomeNotificationService>.Instance);

    [Fact]
    public async Task SendAsync_WhenIdempotencyStoreAlreadyCompleted_SkipsAllWork()
    {
        var store = new InMemoryIdempotencyStore();
        var sut = CreateSutWithStore(store);

        var key = $"activation:{AgentId}:welcome-notification";
        await store.MarkCompletedAsync(key, Ct);

        await sut.SendAsync(AccountId, AgentId, Handle, MakeOutputs(), Ct);

        // Storage, anthropic, and senders never called
        _storage.VerifyNoOtherCalls();
        _anthropic.VerifyNoOtherCalls();
        _whatsApp.VerifyNoOtherCalls();
        _gmail.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_WhenIdempotencyStoreNotCompleted_ProceedsToFileCheck()
    {
        var store = new InMemoryIdempotencyStore();
        var sut = CreateSutWithStore(store);

        // File check returns "already sent" — should still stop after file check
        _storage.Setup(s => s.ReadDocumentAsync(
            $"real-estate-star/{AgentId}", WelcomeNotificationService.WelcomeSentFile, Ct))
            .ReturnsAsync("---\nsent: true\n---\nWelcome message.");

        await sut.SendAsync(AccountId, AgentId, Handle, MakeOutputs(), Ct);

        _anthropic.VerifyNoOtherCalls();
        _whatsApp.VerifyNoOtherCalls();
        _gmail.VerifyNoOtherCalls();
    }
}
