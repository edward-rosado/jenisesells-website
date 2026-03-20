using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Features.WhatsApp;
using RealEstateStar.Api.Features.WhatsApp.Services;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class MultiChannelLeadNotifierTests
{
    private readonly Mock<IWhatsAppNotifier> _whatsApp = new();
    private readonly Mock<IEmailNotifier> _email = new();
    private readonly Mock<IAgentConfigService> _configService = new();
    private readonly Mock<ILogger<MultiChannelLeadNotifier>> _log = new();
    private readonly MultiChannelLeadNotifier _sut;

    private const string AgentId = "jenise-buckalew";

    public MultiChannelLeadNotifierTests()
    {
        _sut = new MultiChannelLeadNotifier(
            _whatsApp.Object,
            _email.Object,
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

    private AgentConfig MakeConfig(bool whatsAppOptedIn = true) =>
        new()
        {
            Id = AgentId,
            Identity = new AgentIdentity { Name = "Jenise Buckalew", Phone = "+12015551234" },
            Integrations = new AgentIntegrations
            {
                WhatsApp = new AgentWhatsApp
                {
                    PhoneNumber = "+12015551234",
                    OptedIn = whatsAppOptedIn,
                    WelcomeSent = true,
                    NotificationPreferences = ["new_lead", "cma_ready", "data_deletion"]
                }
            }
        };

    // -------------------------------------------------------------------------
    // Test 1: WhatsApp opted in → NotifyAsync called with correct params
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_SendsWhatsApp_WhenOptedIn()
    {
        var config = MakeConfig(whatsAppOptedIn: true);
        _configService
            .Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var lead = MakeLead();
        using var cts = new CancellationTokenSource();

        await _sut.NotifyAgentAsync(AgentId, lead, cts.Token);

        _whatsApp.Verify(w => w.NotifyAsync(
            AgentId,
            NotificationType.NewLead,
            lead.Name,
            It.IsAny<Dictionary<string, string>>(),
            cts.Token), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 2: WhatsApp not opted in → NotifyAsync never called
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_SkipsWhatsApp_WhenNotOptedIn()
    {
        var config = MakeConfig(whatsAppOptedIn: false);
        _configService
            .Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        await _sut.NotifyAgentAsync(AgentId, MakeLead(), CancellationToken.None);

        _whatsApp.Verify(w => w.NotifyAsync(
            It.IsAny<string>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 3: WhatsApp throws → email still sent (non-fatal failure)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_WhatsAppFailure_DoesNotBlockEmail()
    {
        var config = MakeConfig(whatsAppOptedIn: true);
        _configService
            .Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _whatsApp
            .Setup(w => w.NotifyAsync(
                It.IsAny<string>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("WhatsApp API timeout"));

        var lead = MakeLead();
        using var cts = new CancellationTokenSource();

        // Must not throw
        Func<Task> act = () => _sut.NotifyAgentAsync(AgentId, lead, cts.Token);
        await act.Should().NotThrowAsync();

        // Email channel must still be called
        _email.Verify(e => e.SendLeadNotificationAsync(
            AgentId,
            lead,
            cts.Token), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 4: No WhatsApp config → skip without error
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_SkipsWhatsApp_WhenNoWhatsAppConfig()
    {
        var config = new AgentConfig
        {
            Id = AgentId,
            Integrations = new AgentIntegrations { WhatsApp = null }
        };
        _configService
            .Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        await _sut.NotifyAgentAsync(AgentId, MakeLead(), CancellationToken.None);

        _whatsApp.Verify(w => w.NotifyAsync(
            It.IsAny<string>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 5: Config load failure → log error, continue (email still attempted)
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_ConfigLoadFailure_LogsAndContinues()
    {
        _configService
            .Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Config file unavailable"));

        var lead = MakeLead();
        using var cts = new CancellationTokenSource();

        Func<Task> act = () => _sut.NotifyAgentAsync(AgentId, lead, cts.Token);
        await act.Should().NotThrowAsync();

        // WhatsApp skipped (no config), email still attempted
        _email.Verify(e => e.SendLeadNotificationAsync(
            AgentId,
            lead,
            cts.Token), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 6a: No Integrations section at all → WhatsApp skipped gracefully
    // Covers the nullable chain branch where Integrations is null
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_SkipsWhatsApp_WhenNoIntegrations()
    {
        var config = new AgentConfig { Id = AgentId, Integrations = null };
        _configService
            .Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        await _sut.NotifyAgentAsync(AgentId, MakeLead(), CancellationToken.None);

        _whatsApp.Verify(w => w.NotifyAsync(
            It.IsAny<string>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 6: Email channel throws → logged, does not propagate
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAgentAsync_EmailFailure_IsLogged_DoesNotThrow()
    {
        var config = MakeConfig(whatsAppOptedIn: false);
        _configService
            .Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _email
            .Setup(e => e.SendLeadNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<LeadNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("SMTP timeout"));

        Func<Task> act = () => _sut.NotifyAgentAsync(AgentId, MakeLead(), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
