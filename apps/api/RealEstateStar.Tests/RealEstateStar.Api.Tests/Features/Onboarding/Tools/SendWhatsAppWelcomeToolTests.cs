using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.DataServices.Config;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class SendWhatsAppWelcomeToolTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SendWhatsAppWelcomeTool CreateTool(
        out Mock<IWhatsAppSender> whatsAppClient,
        out Mock<IAccountConfigService> accountConfigService)
    {
        whatsAppClient = new Mock<IWhatsAppSender>();
        accountConfigService = new Mock<IAccountConfigService>();
        return new SendWhatsAppWelcomeTool(
            whatsAppClient.Object,
            accountConfigService.Object,
            NullLogger<SendWhatsAppWelcomeTool>.Instance);
    }

    private static OnboardingSession MakeSession(string agentConfigId = "agent-001")
    {
        var session = OnboardingSession.Create(null);
        session.AgentConfigId = agentConfigId;
        return session;
    }

    private static AccountConfig MakeAccountConfig(
        string phoneNumber = "+15551234567",
        bool optedIn = true,
        string firstName = "Jenise") => new()
    {
        Handle = "agent-001",
        Agent = new AccountAgent { Name = firstName + " Buckalew", Phone = "555-000-0000", Email = "jenise@example.com" },
        Integrations = new AccountIntegrations
        {
            WhatsApp = new AccountWhatsApp
            {
                PhoneNumber = phoneNumber,
                OptedIn = optedIn,
                Status = "not_registered",
                WelcomeSent = false
            }
        }
    };

    private static readonly JsonElement EmptyParameters = JsonDocument.Parse("{}").RootElement;

    // ── Name property ─────────────────────────────────────────────────────────

    [Fact]
    public void Name_IsSendWhatsAppWelcome()
    {
        var tool = CreateTool(out _, out _);
        Assert.Equal("send_whatsapp_welcome", tool.Name);
    }

    // ── Test 1: Happy path — sends welcome, returns success message ───────────

    [Fact]
    public async Task ExecuteAsync_SendsWelcome_WhenOptedIn()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig();

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        whatsAppClient.Setup(c => c.SendTemplateAsync(
                "+15551234567",
                "welcome_onboarding",
                It.IsAny<List<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.test123");

        var result = await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.Contains("+15551234567", result);
        whatsAppClient.Verify(c => c.SendTemplateAsync(
            "+15551234567",
            "welcome_onboarding",
            It.IsAny<List<(string, string)>>(),
            CancellationToken.None), Times.Once);
    }

    // ── Test 2: Sets status = active and WelcomeSent = true on success ────────

    [Fact]
    public async Task ExecuteAsync_SetsActiveAndWelcomeSent_OnSuccess()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig();

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.ok");

        AccountConfig? savedConfig = null;
        accountConfigService.Setup(s => s.UpdateAccountAsync(
                It.IsAny<string>(), It.IsAny<AccountConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, AccountConfig, CancellationToken>((_, cfg, _) => savedConfig = cfg);

        await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        accountConfigService.Verify(s => s.UpdateAccountAsync("agent-001", It.IsAny<AccountConfig>(), CancellationToken.None), Times.Once);
        Assert.NotNull(savedConfig);
        Assert.Equal("active", savedConfig!.Integrations!.WhatsApp!.Status);
        Assert.True(savedConfig.Integrations.WhatsApp.WelcomeSent);
    }

    // ── Test 3: Returns email fallback when WhatsApp not configured ───────────

    [Theory]
    [InlineData(false, "+15551234567")]  // opted_in = false
    [InlineData(true, "")]              // empty phone
    public async Task ExecuteAsync_ReturnsEmailFallback_WhenNotConfigured(bool optedIn, string phone)
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig(phoneNumber: phone, optedIn: optedIn);

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.Contains("email", result, StringComparison.OrdinalIgnoreCase);
        whatsAppClient.Verify(c => c.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmailFallback_WhenWhatsAppIsNull()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = new AccountConfig
        {
            Handle = "agent-001",
            Agent = new AccountAgent { Name = "Jenise B", Phone = "555-0000", Email = "j@example.com" },
            Integrations = new AccountIntegrations { WhatsApp = null }
        };

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.Contains("email", result, StringComparison.OrdinalIgnoreCase);
        whatsAppClient.Verify(c => c.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Test 4: Sets status = not_registered and schedules RetryAfter ─────────

    [Fact]
    public async Task ExecuteAsync_SetsNotRegistered_OnWhatsAppNotRegistered()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig();
        var before = DateTime.UtcNow;

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException("+15551234567"));

        AccountConfig? savedConfig = null;
        accountConfigService.Setup(s => s.UpdateAccountAsync(
                It.IsAny<string>(), It.IsAny<AccountConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, AccountConfig, CancellationToken>((_, cfg, _) => savedConfig = cfg);

        await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.NotNull(savedConfig);
        Assert.Equal("not_registered", savedConfig!.Integrations!.WhatsApp!.Status);
        Assert.NotNull(savedConfig.Integrations.WhatsApp.RetryAfter);
        Assert.True(savedConfig.Integrations.WhatsApp.RetryAfter >= before.AddHours(4));
        accountConfigService.Verify(s => s.UpdateAccountAsync("agent-001", It.IsAny<AccountConfig>(), CancellationToken.None), Times.Once);
    }

    // ── Test 5: Returns retry message on WhatsAppNotRegisteredException ────────

    [Fact]
    public async Task ExecuteAsync_ReturnsRetryMessage_OnNotRegistered()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig();

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException("+15551234567"));

        var result = await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.Contains("try again", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 6: Sets status = error on other exceptions ────────────────────────

    [Fact]
    public async Task ExecuteAsync_SetsError_OnOtherException()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig();

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppApiException(131026, "Too many messages"));

        AccountConfig? savedConfig = null;
        accountConfigService.Setup(s => s.UpdateAccountAsync(
                It.IsAny<string>(), It.IsAny<AccountConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, AccountConfig, CancellationToken>((_, cfg, _) => savedConfig = cfg);

        await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.NotNull(savedConfig);
        Assert.Equal("error", savedConfig!.Integrations!.WhatsApp!.Status);
        accountConfigService.Verify(s => s.UpdateAccountAsync("agent-001", It.IsAny<AccountConfig>(), CancellationToken.None), Times.Once);
    }

    // ── Test 7: Returns email fallback message on other exceptions ─────────────

    [Fact]
    public async Task ExecuteAsync_ReturnsEmailFallback_OnError()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig();

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected internal failure"));

        var result = await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.Contains("email", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ReturnsEmailFallback_WhenAccountConfigIsNull()
    {
        var tool = CreateTool(out _, out var accountConfigService);
        var session = MakeSession();

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

        var result = await tool.ExecuteAsync(EmptyParameters, session, CancellationToken.None);

        Assert.Contains("email", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken_ToSendTemplate()
    {
        var tool = CreateTool(out var whatsAppClient, out var accountConfigService);
        var session = MakeSession();
        var config = MakeAccountConfig();
        using var cts = new CancellationTokenSource();

        accountConfigService.Setup(s => s.GetAccountAsync("agent-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.ok");

        await tool.ExecuteAsync(EmptyParameters, session, cts.Token);

        whatsAppClient.Verify(c => c.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), cts.Token), Times.Once);
    }
}
