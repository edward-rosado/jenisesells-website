using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.WhatsApp;
using RealEstateStar.Api.Features.WhatsApp.Services;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class WhatsAppNotifierTests : IDisposable
{
    private readonly Mock<IWhatsAppClient> _client = new();
    private readonly Mock<IConversationLogger> _logger = new();
    private readonly Mock<IAgentConfigService> _configService = new();
    private readonly Mock<ILogger<WhatsAppNotifier>> _log = new();
    private readonly IMemoryCache _cache;
    private readonly WhatsAppNotifier _sut;

    private const string AgentId = "jenise-buckalew";
    private const string AgentPhone = "+12015551234";

    public WhatsAppNotifierTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new WhatsAppNotifier(_client.Object, _logger.Object,
            _configService.Object, _cache, _log.Object);
    }

    public void Dispose() => _cache.Dispose();

    private AgentConfig MakeConfig(
        bool optedIn = true,
        bool welcomeSent = true,
        List<string>? preferences = null) =>
        new()
        {
            Id = AgentId,
            Identity = new AgentIdentity { Name = "Jenise Buckalew", Phone = AgentPhone },
            Integrations = new AgentIntegrations
            {
                WhatsApp = new AgentWhatsApp
                {
                    PhoneNumber = AgentPhone,
                    OptedIn = optedIn,
                    WelcomeSent = welcomeSent,
                    NotificationPreferences = preferences ?? ["new_lead", "cma_ready", "data_deletion"]
                }
            }
        };

    // -------------------------------------------------------------------------
    // Test 1: Happy path — opted in, preference set, window closed → template
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_SendsTemplate_WhenAgentOptedIn()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.1");

        var @params = new Dictionary<string, string> { ["lead_name"] = "Jane" };
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "new_lead_notification",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 2: Not opted in — skip entirely
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Skips_WhenNotOptedIn()
    {
        var config = MakeConfig(optedIn: false);
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, null, @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
        _client.Verify(c => c.SendFreeformAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 3: Type not in preferences — skip
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Skips_WhenTypeNotInPreferences()
    {
        // FollowUpReminder not in default preferences ["new_lead", "cma_ready", "data_deletion"]
        var config = MakeConfig();
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.FollowUpReminder, null, @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 4: Welcome not sent — welcome template sent first, then notification
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_SendsWelcome_WhenWelcomeSentIsFalse()
    {
        var config = MakeConfig(welcomeSent: false);
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.x");

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, null, @params, CancellationToken.None);

        // Welcome sent first
        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "welcome",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
        // Then the actual notification
        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "new_lead_notification",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 5: Welcome sent — config updated with welcome_sent = true
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_UpdatesWelcomeSentOnSuccess()
    {
        var config = MakeConfig(welcomeSent: false);
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.x");

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, null, @params, CancellationToken.None);

        _configService.Verify(s => s.UpdateAgentAsync(
            AgentId,
            It.Is<AgentConfig>(c => c.Integrations!.WhatsApp!.WelcomeSent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 6: WhatsAppNotRegisteredException — log [WA-014], no throw
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_FallsGracefully_OnNotRegistered()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException(AgentPhone));

        var @params = new Dictionary<string, string>();
        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null, @params, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _log.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("WA-014")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 7: Conversation logged after sending
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_LogsConversation_AfterSending()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.2");

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, "Jane", @params, CancellationToken.None);

        _logger.Verify(l => l.LogMessagesAsync(
            AgentId,
            "Jane",
            It.IsAny<List<(DateTime, string, string, string?)>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 8: DataDeletion always sends even if not in preferences
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_DataDeletion_AlwaysSends()
    {
        // Preferences explicitly excluding data_deletion
        var config = MakeConfig(preferences: ["new_lead"]);
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.3");

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.DataDeletion, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "data_deletion_notice",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 9: Window closed for unknown agent
    // -------------------------------------------------------------------------
    [Fact]
    public void IsWindowOpen_ReturnsFalse_ForUnknownAgent()
    {
        var result = _sut.IsWindowOpen("unknown-phone");

        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Test 10: RecordAgentMessage sets window, IsWindowOpen returns true
    // -------------------------------------------------------------------------
    [Fact]
    public void RecordAgentMessage_ThenIsWindowOpen_ReturnsTrue()
    {
        _sut.RecordAgentMessage(AgentPhone);

        _sut.IsWindowOpen(AgentPhone).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Test 11: Window open → SendFreeformAsync used instead of SendTemplateAsync
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_SendsFreeform_WhenWindowOpen()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.4");

        _sut.RecordAgentMessage(AgentPhone);

        var @params = new Dictionary<string, string> { ["lead_name"] = "Jane" };
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _client.Verify(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 12: Window closed → SendTemplateAsync used
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_SendsTemplate_WhenWindowClosed()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAgentAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.5");

        // No RecordAgentMessage call — window is closed

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, null, @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "new_lead_notification",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
        _client.Verify(c => c.SendFreeformAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
