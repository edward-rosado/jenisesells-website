using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.DataServices.Config;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class CascadingAgentNotifierTests
{
    private readonly Mock<IWhatsAppNotifier> _whatsApp = new();
    private readonly Mock<IEmailNotifier> _email = new();
    private readonly Mock<IConversationLogger> _conversationLogger = new();
    private readonly Mock<IAccountConfigService> _configService = new();
    private readonly Mock<ILogger<CascadingAgentNotifier>> _log = new();
    private readonly CascadingAgentNotifier _sut;

    private const string AgentId = "jenise-buckalew";

    public CascadingAgentNotifierTests()
    {
        _sut = new CascadingAgentNotifier(
            _whatsApp.Object,
            _email.Object,
            _conversationLogger.Object,
            _configService.Object,
            _log.Object);
    }

    private static LeadNotification MakeLead(
        string name = "Jane Smith",
        string phone = "555-9999",
        string email = "jane@example.com",
        string interest = "Buying",
        string area = "Montclair, NJ") =>
        new(name, phone, email, interest, area);

    private AccountConfig MakeConfig(bool whatsAppOptedIn = true) =>
        new()
        {
            Handle = AgentId,
            Agent = new AccountAgent { Name = "Jenise Buckalew", Phone = "+12015551234" },
            Integrations = new AccountIntegrations
            {
                WhatsApp = new AccountWhatsApp
                {
                    PhoneNumber = "+12015551234",
                    OptedIn = whatsAppOptedIn,
                    WelcomeSent = true,
                    NotificationPreferences = ["new_lead", "cma_ready", "data_deletion"]
                }
            }
        };

    // -------------------------------------------------------------------------
    // WhatsApp succeeds → email NOT sent, file storage NOT used
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_WhatsAppSucceeds_EmailAndFileStorageNotUsed()
    {
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(whatsAppOptedIn: true));

        await _sut.NotifyAgentAsync(AgentId, MakeLead(), CancellationToken.None);

        _whatsApp.Verify(w => w.NotifyAsync(
            AgentId, NotificationType.NewLead, "Jane Smith",
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _email.Verify(e => e.SendLeadNotificationAsync(
            It.IsAny<string>(), It.IsAny<LeadNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _conversationLogger.Verify(c => c.LogMessagesAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<List<(DateTime, string, string, string?)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // WhatsApp not opted in → falls back to email, no file storage
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_WhatsAppNotOptedIn_FallsBackToEmail()
    {
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(whatsAppOptedIn: false));

        var lead = MakeLead();
        await _sut.NotifyAgentAsync(AgentId, lead, CancellationToken.None);

        _email.Verify(e => e.SendLeadNotificationAsync(
            AgentId, lead, It.IsAny<CancellationToken>()), Times.Once);

        _conversationLogger.Verify(c => c.LogMessagesAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<List<(DateTime, string, string, string?)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // WhatsApp fails → falls back to email, no file storage
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_WhatsAppFails_FallsBackToEmail()
    {
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(whatsAppOptedIn: true));

        _whatsApp
            .Setup(w => w.NotifyAsync(
                It.IsAny<string>(), It.IsAny<NotificationType>(),
                It.IsAny<string?>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("WhatsApp API timeout"));

        var lead = MakeLead();
        await _sut.NotifyAgentAsync(AgentId, lead, CancellationToken.None);

        _email.Verify(e => e.SendLeadNotificationAsync(
            AgentId, lead, It.IsAny<CancellationToken>()), Times.Once);

        _conversationLogger.Verify(c => c.LogMessagesAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<List<(DateTime, string, string, string?)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Both WhatsApp and email fail → falls back to file storage
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_BothFail_FallsBackToFileStorage()
    {
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(whatsAppOptedIn: true));

        _whatsApp
            .Setup(w => w.NotifyAsync(
                It.IsAny<string>(), It.IsAny<NotificationType>(),
                It.IsAny<string?>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("WhatsApp down"));

        _email
            .Setup(e => e.SendLeadNotificationAsync(
                It.IsAny<string>(), It.IsAny<LeadNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP down"));

        var lead = MakeLead();
        Func<Task> act = () => _sut.NotifyAgentAsync(AgentId, lead, CancellationToken.None);
        await act.Should().NotThrowAsync();

        _conversationLogger.Verify(c => c.LogMessagesAsync(
            AgentId, lead.Name,
            It.IsAny<List<(DateTime, string, string, string?)>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // WhatsApp not configured + email fails → falls back to file storage
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_NoWhatsAppAndEmailFails_FallsBackToFileStorage()
    {
        var config = new AccountConfig
        {
            Handle = AgentId,
            Integrations = new AccountIntegrations { WhatsApp = null }
        };
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _email
            .Setup(e => e.SendLeadNotificationAsync(
                It.IsAny<string>(), It.IsAny<LeadNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP down"));

        var lead = MakeLead();
        Func<Task> act = () => _sut.NotifyAgentAsync(AgentId, lead, CancellationToken.None);
        await act.Should().NotThrowAsync();

        _conversationLogger.Verify(c => c.LogMessagesAsync(
            AgentId, lead.Name,
            It.IsAny<List<(DateTime, string, string, string?)>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // All three channels fail → logged, does not throw
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_AllChannelsFail_DoesNotThrow()
    {
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(whatsAppOptedIn: true));

        _whatsApp
            .Setup(w => w.NotifyAsync(
                It.IsAny<string>(), It.IsAny<NotificationType>(),
                It.IsAny<string?>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("WhatsApp down"));

        _email
            .Setup(e => e.SendLeadNotificationAsync(
                It.IsAny<string>(), It.IsAny<LeadNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP down"));

        _conversationLogger
            .Setup(c => c.LogMessagesAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<List<(DateTime, string, string, string?)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Drive down"));

        Func<Task> act = () => _sut.NotifyAgentAsync(AgentId, MakeLead(), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // No Integrations at all → falls back to email
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_NoIntegrations_FallsBackToEmail()
    {
        var config = new AccountConfig { Handle = AgentId, Integrations = null };
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var lead = MakeLead();
        await _sut.NotifyAgentAsync(AgentId, lead, CancellationToken.None);

        _email.Verify(e => e.SendLeadNotificationAsync(
            AgentId, lead, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Config load failure → email still fires
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_ConfigLoadFailure_FallsBackToEmail()
    {
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Config file unavailable"));

        var lead = MakeLead();
        Func<Task> act = () => _sut.NotifyAgentAsync(AgentId, lead, CancellationToken.None);
        await act.Should().NotThrowAsync();

        _email.Verify(e => e.SendLeadNotificationAsync(
            AgentId, lead, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Email fallback succeeds → file storage NOT used
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_EmailSucceeds_FileStorageNotUsed()
    {
        _configService
            .Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(whatsAppOptedIn: false));

        var lead = MakeLead();
        await _sut.NotifyAgentAsync(AgentId, lead, CancellationToken.None);

        _email.Verify(e => e.SendLeadNotificationAsync(
            AgentId, lead, It.IsAny<CancellationToken>()), Times.Once);

        _conversationLogger.Verify(c => c.LogMessagesAsync(
            It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<List<(DateTime, string, string, string?)>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
