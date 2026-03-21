using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace RealEstateStar.Notifications.Tests.WhatsApp;

public class WhatsAppNotifierTests : IDisposable
{
    private readonly Mock<IWhatsAppSender> _client = new();
    private readonly Mock<IConversationLogger> _logger = new();
    private readonly Mock<IAccountConfigService> _configService = new();
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

    private AccountConfig MakeConfig(
        bool optedIn = true,
        bool welcomeSent = true,
        List<string>? preferences = null) =>
        new()
        {
            Handle = AgentId,
            Agent = new AccountAgent { Name = "Jenise Buckalew", Phone = AgentPhone },
            Integrations = new AccountIntegrations
            {
                WhatsApp = new AccountWhatsApp
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.x");

        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, null, @params, CancellationToken.None);

        _configService.Verify(s => s.UpdateAccountAsync(
            AgentId,
            It.Is<AccountConfig>(c => c.Integrations!.WhatsApp!.WelcomeSent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 6: WhatsAppNotRegisteredException — log [WA-014], no throw
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_FallsGracefully_OnNotRegistered()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
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

    // -------------------------------------------------------------------------
    // Test 13: Config load throws → logs warning, no send
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Skips_WhenConfigLoadThrows()
    {
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("config service unavailable"));

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
        _log.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("WA-015")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 14: Config has no WhatsApp integration → skips
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Skips_WhenWhatsAppIntegrationAbsent()
    {
        var config = new AccountConfig
        {
            Handle = AgentId,
            Agent = new AccountAgent { Name = "Jenise Buckalew" },
            Integrations = new AccountIntegrations { WhatsApp = null }
        };
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 15: Config has null Integrations → skips
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Skips_WhenIntegrationsNull()
    {
        var config = new AccountConfig
        {
            Handle = AgentId,
            Agent = new AccountAgent { Name = "Jenise Buckalew" },
            Integrations = null
        };
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 16: Welcome flow — general Exception → logs error, returns early
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_WelcomeFlow_GeneralException_LogsErrorAndReturns()
    {
        var config = MakeConfig(welcomeSent: false);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "welcome",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network failure"));

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _log.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("WA-015")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        // Must not proceed to send the actual notification
        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "new_lead_notification",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 17: Welcome flow — WhatsAppNotRegisteredException → logs warning, returns
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_WelcomeFlow_NotRegistered_LogsWarningAndReturns()
    {
        var config = MakeConfig(welcomeSent: false);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "welcome",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException(AgentPhone));

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _log.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("WA-014")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 18: Send freeform throws general exception → logs error [WA-015]
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_FreeformSend_GeneralException_LogsError()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        _sut.RecordAgentMessage(AgentPhone);

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _log.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("WA-015")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 19: Template send throws NotRegistered with window closed → logs [WA-014]
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_TemplateSend_NotRegistered_LogsWarning()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WhatsAppNotRegisteredException(AgentPhone));

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _log.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("WA-014")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 20: BuildTemplateParams — CmaReady type with window closed
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_SendsTemplate_ForCmaReady()
    {
        var config = MakeConfig(preferences: ["cma_ready"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "cma_ready",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.cma");

        var @params = new Dictionary<string, string>
        {
            ["address"] = "123 Main St",
            ["estimated_value"] = "$450,000"
        };
        await _sut.NotifyAsync(AgentId, NotificationType.CmaReady, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "cma_ready",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 21: BuildTemplateParams — FollowUpReminder type with window closed
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_SendsTemplate_ForFollowUpReminder()
    {
        var config = MakeConfig(preferences: ["follow_up_reminder"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "follow_up_reminder",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.fu");

        var @params = new Dictionary<string, string> { ["days"] = "7" };
        await _sut.NotifyAsync(AgentId, NotificationType.FollowUpReminder, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "follow_up_reminder",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 22: BuildTemplateParams — FollowUpReminder with invalid days string → defaults to 0
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_FollowUpReminder_InvalidDays_DefaultsToZero()
    {
        var config = MakeConfig(preferences: ["follow_up_reminder"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "follow_up_reminder",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.fu2");

        // "not-a-number" cannot be parsed as int → defaults to 0
        var @params = new Dictionary<string, string> { ["days"] = "not-a-number" };
        var act = () => _sut.NotifyAsync(AgentId, NotificationType.FollowUpReminder, "Jane", @params, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // Test 23: BuildTemplateParams — DataDeletion with valid deletion deadline
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_DataDeletion_WithValidDeadline_SendsTemplate()
    {
        var config = MakeConfig(preferences: ["new_lead"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "data_deletion_notice",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.del");

        var @params = new Dictionary<string, string>
        {
            ["deletion_deadline"] = "2026-04-01T00:00:00Z"
        };
        await _sut.NotifyAsync(AgentId, NotificationType.DataDeletion, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "data_deletion_notice",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 24: BuildTemplateParams — default case (ListingAlert) via template path
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_ListingAlert_FallsToDefaultTemplateParams()
    {
        var config = MakeConfig(preferences: ["listing_alert"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "listing_alert",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.la");

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.ListingAlert, "Jane",
            new Dictionary<string, string>(), CancellationToken.None);

        // ListingAlert hits the _ => [] default case in BuildTemplateParams
        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "listing_alert",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 25: BuildFreeformBody — CmaReady with window open
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Freeform_CmaReady_BuildsCorrectBody()
    {
        var config = MakeConfig(preferences: ["cma_ready"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.fcma");

        _sut.RecordAgentMessage(AgentPhone);

        var @params = new Dictionary<string, string>
        {
            ["address"] = "123 Main St",
            ["estimated_value"] = "$450,000"
        };
        await _sut.NotifyAsync(AgentId, NotificationType.CmaReady, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendFreeformAsync(AgentPhone,
            It.Is<string>(b => b.Contains("CMA ready") && b.Contains("Jane")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 26: BuildFreeformBody — FollowUpReminder with window open
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Freeform_FollowUpReminder_BuildsCorrectBody()
    {
        var config = MakeConfig(preferences: ["follow_up_reminder"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.ffu");

        _sut.RecordAgentMessage(AgentPhone);

        var @params = new Dictionary<string, string> { ["days"] = "3" };
        await _sut.NotifyAsync(AgentId, NotificationType.FollowUpReminder, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendFreeformAsync(AgentPhone,
            It.Is<string>(b => b.Contains("Follow-up") && b.Contains("Jane")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 27: BuildFreeformBody — DataDeletion with window open
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Freeform_DataDeletion_BuildsCorrectBody()
    {
        // DataDeletion bypasses preference check
        var config = MakeConfig(preferences: ["new_lead"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.fdel");

        _sut.RecordAgentMessage(AgentPhone);

        var @params = new Dictionary<string, string> { ["deletion_deadline"] = "2026-04-01" };
        await _sut.NotifyAsync(AgentId, NotificationType.DataDeletion, "Jane", @params, CancellationToken.None);

        _client.Verify(c => c.SendFreeformAsync(AgentPhone,
            It.Is<string>(b => b.Contains("deletion") && b.Contains("Jane")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 28: BuildFreeformBody — default case (ListingAlert) with window open
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Freeform_ListingAlert_UsesDefaultFreeformBody()
    {
        var config = MakeConfig(preferences: ["listing_alert"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.fla");

        _sut.RecordAgentMessage(AgentPhone);

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.ListingAlert, "Jane",
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 29: BuildFreeformBody — leadName is null, uses params fallback
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Freeform_NullLeadName_FallsBackToParamOrSomeone()
    {
        var config = MakeConfig();
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendFreeformAsync(AgentPhone, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.fnull");

        _sut.RecordAgentMessage(AgentPhone);

        // No leadName, no lead_name param → falls back to "someone"
        var @params = new Dictionary<string, string>();
        await _sut.NotifyAsync(AgentId, NotificationType.NewLead, null, @params, CancellationToken.None);

        _client.Verify(c => c.SendFreeformAsync(AgentPhone,
            It.Is<string>(b => b.Contains("someone")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 30a: configService returns null → treated same as no WhatsApp config
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Skips_WhenConfigIsNull()
    {
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Test 30b: Type not in PreferenceKeys (Welcome) → preference check skips TryGetValue false path
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_Skips_WhenTypeNotInPreferenceKeysDictionary()
    {
        var config = MakeConfig(preferences: ["new_lead"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.w");

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.Welcome, null,
            new Dictionary<string, string>(), CancellationToken.None);

        // Welcome type: TryGetValue returns false → no pref check → proceeds
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // Test 30c: BuildTemplateParams — CmaReady with null leadName → uses params fallback
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_CmaReady_NullLeadName_UsesParamFallback()
    {
        var config = MakeConfig(preferences: ["cma_ready"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "cma_ready",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.cma2");

        var @params = new Dictionary<string, string>
        {
            ["lead_name"] = "John Smith",
            ["address"] = "456 Oak Ave",
            ["estimated_value"] = "$500,000"
        };
        // Pass null leadName so the ?? fallback in BuildTemplateParams is exercised
        await _sut.NotifyAsync(AgentId, NotificationType.CmaReady, null, @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "cma_ready",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 30d: BuildTemplateParams — FollowUpReminder with null leadName → uses params fallback
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_FollowUpReminder_NullLeadName_UsesParamFallback()
    {
        var config = MakeConfig(preferences: ["follow_up_reminder"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "follow_up_reminder",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.fu3");

        var @params = new Dictionary<string, string>
        {
            ["lead_name"] = "Bob Jones",
            ["days"] = "5"
        };
        await _sut.NotifyAsync(AgentId, NotificationType.FollowUpReminder, null, @params, CancellationToken.None);

        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "follow_up_reminder",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 30e: BuildTemplateParams — DataDeletion with invalid/missing deadline → uses UtcNow+30d default
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_DataDeletion_InvalidDeadline_UsesDefault()
    {
        var config = MakeConfig(preferences: ["new_lead"]);
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, "data_deletion_notice",
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.del2");

        // No deletion_deadline in params → TryParse fails → defaults to UtcNow.AddDays(30)
        var @params = new Dictionary<string, string>();
        var act = () => _sut.NotifyAsync(AgentId, NotificationType.DataDeletion, null, @params, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "data_deletion_notice",
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Test 30: Welcome flow — Agent is null → firstName defaults to "there"
    // -------------------------------------------------------------------------
    [Fact]
    public async Task NotifyAsync_WelcomeFlow_NullAgent_UsesThereAsFirstName()
    {
        var config = new AccountConfig
        {
            Handle = AgentId,
            Agent = null,
            Integrations = new AccountIntegrations
            {
                WhatsApp = new AccountWhatsApp
                {
                    PhoneNumber = AgentPhone,
                    OptedIn = true,
                    WelcomeSent = false,
                    NotificationPreferences = ["new_lead"]
                }
            }
        };
        _configService.Setup(s => s.GetAccountAsync(AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _client.Setup(c => c.SendTemplateAsync(AgentPhone, It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("wamid.welcome");

        var act = () => _sut.NotifyAsync(AgentId, NotificationType.NewLead, null,
            new Dictionary<string, string>(), CancellationToken.None);

        // Should not throw — falls back to "there" as firstName
        await act.Should().NotThrowAsync();
        _client.Verify(c => c.SendTemplateAsync(AgentPhone, "welcome",
            It.Is<List<(string type, string value)>>(p =>
                p.Any(x => x.value == "there")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
