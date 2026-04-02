using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Functions.WhatsApp;
using RealEstateStar.Workers.WhatsApp;

namespace RealEstateStar.Functions.Tests.WhatsApp;

public class WhatsAppRetryFunctionTests
{
    private readonly Mock<IAccountConfigService> _configService = new();
    private readonly Mock<IWhatsAppSender> _whatsAppSender = new();
    private readonly Mock<ILogger<WhatsAppRetryJob>> _retryJobLogger = new();
    private readonly Mock<ILogger<WhatsAppRetryFunction>> _functionLogger = new();

    private WhatsAppRetryFunction CreateSut()
    {
        var retryJob = new WhatsAppRetryJob(
            _configService.Object,
            _whatsAppSender.Object,
            _retryJobLogger.Object);
        return new WhatsAppRetryFunction(retryJob, _functionLogger.Object);
    }

    private static TimerInfo MakeTimerInfo(bool isPastDue = false) =>
        new TimerInfo { IsPastDue = isPastDue };

    [Fact]
    public async Task RunAsync_DelegatesTo_RetryJob_ProcessRetriesAsync()
    {
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountConfig>());

        var sut = CreateSut();
        await sut.RunAsync(MakeTimerInfo(), CancellationToken.None);

        _configService.Verify(s => s.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ProcessesRetries_WhenAccountHasRetryAfterInPast()
    {
        var config = MakeConfig(retryAfter: DateTime.UtcNow.AddMinutes(-5));
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountConfig> { config });
        _whatsAppSender.Setup(s => s.SendTemplateAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.ok");

        var sut = CreateSut();
        await sut.RunAsync(MakeTimerInfo(), CancellationToken.None);

        _whatsAppSender.Verify(s => s.SendTemplateAsync(
            "+12015551234", "welcome_onboarding",
            It.IsAny<List<(string, string)>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_Succeeds_WhenAccountListIsEmpty()
    {
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountConfig>());

        var sut = CreateSut();
        // Should not throw
        await sut.RunAsync(MakeTimerInfo(), CancellationToken.None);

        _whatsAppSender.Verify(s => s.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_RethrowsException_OnRetryJobFailure()
    {
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        var sut = CreateSut();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RunAsync(MakeTimerInfo(), CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_LogsTimerFired_WithIsPastDue()
    {
        _configService.Setup(s => s.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountConfig>());

        var sut = CreateSut();
        // pastDue = true — function should still run normally
        await sut.RunAsync(MakeTimerInfo(isPastDue: true), CancellationToken.None);

        _configService.Verify(s => s.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AccountConfig MakeConfig(
        bool optedIn = true,
        bool welcomeSent = false,
        DateTime? retryAfter = null,
        string phone = "+12015551234",
        string name = "Jenise Buckalew",
        string handle = "test-agent")
    {
        return new AccountConfig
        {
            Handle = handle,
            Agent = new AccountAgent { Name = name, Phone = phone, Email = "jenise@example.com" },
            Integrations = new AccountIntegrations
            {
                WhatsApp = new AccountWhatsApp
                {
                    PhoneNumber = phone,
                    OptedIn = optedIn,
                    WelcomeSent = welcomeSent,
                    RetryAfter = retryAfter
                }
            }
        };
    }
}
