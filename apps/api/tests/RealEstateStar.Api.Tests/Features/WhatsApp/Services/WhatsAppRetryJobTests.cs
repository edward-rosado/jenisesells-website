using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Api.Features.WhatsApp.Services;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class WhatsAppRetryJobTests
{
    private readonly Mock<IAccountConfigService> _configService = new();
    private readonly Mock<IWhatsAppSender> _whatsAppClient = new();
    private readonly Mock<ILogger<WhatsAppRetryJob>> _logger = new();

    private WhatsAppRetryJob CreateSut() =>
        new(_configService.Object, _whatsAppClient.Object, _logger.Object);

    private static AccountConfig MakeConfig(
        bool optedIn = true,
        bool welcomeSent = false,
        string status = "not_registered",
        DateTime? retryAfter = null,
        string phone = "+12015551234",
        string name = "Jenise Buckalew",
        string handle = "test-agent")
    {
        var wa = new AccountWhatsApp
        {
            PhoneNumber = phone,
            OptedIn = optedIn,
            WelcomeSent = welcomeSent,
            Status = status,
            RetryAfter = retryAfter
        };

        return new AccountConfig
        {
            Handle = handle,
            Agent = new AccountAgent { Name = name, Phone = phone, Email = "jenise@example.com" },
            Integrations = new AccountIntegrations { WhatsApp = wa }
        };
    }

    [Fact]
    public async Task ProcessRetries_SendsWelcome_WhenRetryAfterInPast()
    {
        var config = MakeConfig(retryAfter: DateTime.UtcNow.AddMinutes(-5));
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([config]);
        _whatsAppClient.Setup(c => c.SendTemplateAsync(
                "+12015551234", "welcome_onboarding",
                It.IsAny<List<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.abc");

        var sut = CreateSut();
        await sut.ProcessRetries(CancellationToken.None);

        _whatsAppClient.Verify(c => c.SendTemplateAsync(
            "+12015551234", "welcome_onboarding",
            It.IsAny<List<(string, string)>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRetries_SkipsAgent_WhenRetryAfterInFuture()
    {
        var config = MakeConfig(retryAfter: DateTime.UtcNow.AddHours(2));
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([config]);

        var sut = CreateSut();
        await sut.ProcessRetries(CancellationToken.None);

        _whatsAppClient.Verify(c => c.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRetries_SkipsAgent_WhenNotOptedIn()
    {
        var config = MakeConfig(optedIn: false, retryAfter: DateTime.UtcNow.AddMinutes(-1));
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([config]);

        var sut = CreateSut();
        await sut.ProcessRetries(CancellationToken.None);

        _whatsAppClient.Verify(c => c.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRetries_SkipsAgent_WhenWelcomeSent()
    {
        var config = MakeConfig(welcomeSent: true, retryAfter: DateTime.UtcNow.AddMinutes(-1));
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([config]);

        var sut = CreateSut();
        await sut.ProcessRetries(CancellationToken.None);

        _whatsAppClient.Verify(c => c.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRetries_OnSuccess_SetsActiveAndWelcomeSent()
    {
        var config = MakeConfig(retryAfter: DateTime.UtcNow.AddMinutes(-5));
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([config]);
        _whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.ok");

        var sut = CreateSut();
        await sut.ProcessRetries(CancellationToken.None);

        _configService.Verify(s => s.UpdateAccountAsync(
            "test-agent",
            It.Is<AccountConfig>(c =>
                c.Integrations!.WhatsApp!.Status == "active" &&
                c.Integrations.WhatsApp.WelcomeSent == true &&
                c.Integrations.WhatsApp.RetryAfter == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRetries_OnFailure_ClearsRetryAfter()
    {
        var config = MakeConfig(retryAfter: DateTime.UtcNow.AddMinutes(-5));
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([config]);
        _whatsAppClient.Setup(c => c.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppApiException(131047, "Re-engagement message"));

        var sut = CreateSut();
        await sut.ProcessRetries(CancellationToken.None);

        _configService.Verify(s => s.UpdateAccountAsync(
            "test-agent",
            It.Is<AccountConfig>(c => c.Integrations!.WhatsApp!.RetryAfter == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRetries_ProcessesMultipleAgents_Independently()
    {
        var configA = MakeConfig(retryAfter: DateTime.UtcNow.AddMinutes(-5), phone: "+12015551234", name: "Jenise Buckalew", handle: "agent-a");
        var configB = MakeConfig(retryAfter: DateTime.UtcNow.AddMinutes(-10), phone: "+15555550001", name: "Bob Smith", handle: "agent-b");

        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([configA, configB]);

        // agent-a fails, agent-b succeeds
        _whatsAppClient.Setup(c => c.SendTemplateAsync(
                "+12015551234", "welcome_onboarding",
                It.IsAny<List<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException("+12015551234"));
        _whatsAppClient.Setup(c => c.SendTemplateAsync(
                "+15555550001", "welcome_onboarding",
                It.IsAny<List<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.bob");

        var sut = CreateSut();
        await sut.ProcessRetries(CancellationToken.None);

        // agent-a: retry_after cleared (failure path)
        _configService.Verify(s => s.UpdateAccountAsync(
            "agent-a",
            It.Is<AccountConfig>(c => c.Integrations!.WhatsApp!.RetryAfter == null &&
                                    c.Integrations.WhatsApp.WelcomeSent == false),
            It.IsAny<CancellationToken>()), Times.Once);

        // agent-b: active + welcome sent
        _configService.Verify(s => s.UpdateAccountAsync(
            "agent-b",
            It.Is<AccountConfig>(c => c.Integrations!.WhatsApp!.Status == "active" &&
                                    c.Integrations.WhatsApp.WelcomeSent == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
