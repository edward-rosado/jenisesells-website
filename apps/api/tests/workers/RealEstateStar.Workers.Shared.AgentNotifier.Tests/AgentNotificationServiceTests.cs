using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;

namespace RealEstateStar.Workers.Shared.AgentNotifier.Tests;

public class AgentNotificationServiceTests
{
    private readonly Mock<IWhatsAppSender> _whatsAppSender = new();
    private readonly Mock<IGmailSender> _gmailSender = new();
    private readonly Mock<ILogger<AgentNotificationService>> _logger = new();
    private readonly AgentNotificationService _notifier;

    private static readonly CancellationToken Ct = CancellationToken.None;

    public AgentNotificationServiceTests()
    {
        _notifier = new AgentNotificationService(_whatsAppSender.Object, _gmailSender.Object, _logger.Object);
    }

    private static AgentNotificationConfig MakeConfig(string? whatsAppPhoneNumberId = "123456789") =>
        new()
        {
            AgentId = "jenise-buckalew",
            Handle = "jenise",
            Name = "Jenise Buckalew",
            FirstName = "Jenise",
            Email = "jenise@example.com",
            Phone = "555-0100",
            LicenseNumber = "NJ-12345",
            BrokerageName = "Keller Williams",
            PrimaryColor = "#2563eb",
            AccentColor = "#1e40af",
            State = "NJ",
            WhatsAppPhoneNumberId = whatsAppPhoneNumberId
        };

    private static Lead MakeLead(SellerDetails? seller = null, BuyerDetails? buyer = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = "jenise-buckalew",
            LeadType = seller is not null ? LeadType.Seller : LeadType.Buyer,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "555-1234",
            Timeline = "3-6 months",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = seller,
            BuyerDetails = buyer
        };

    private static LeadScore MakeScore(int overall = 75) =>
        new()
        {
            OverallScore = overall,
            Factors = [],
            Explanation = "Strong buyer intent"
        };

    // ─── Test 1: WhatsApp configured + succeeds → done, no email sent ───────

    [Fact]
    public async Task NotifyAsync_WhatsAppConfiguredAndSucceeds_SendsWhatsAppAndSkipsEmail()
    {
        var lead = MakeLead();
        var score = MakeScore();
        var config = MakeConfig();

        _whatsAppSender
            .Setup(x => x.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), Ct))
            .ReturnsAsync("msg-id-123");

        await _notifier.NotifyAsync(lead, score, null, null, config, Ct);

        _whatsAppSender.Verify(x => x.SendTemplateAsync(
            config.WhatsAppPhoneNumberId!,
            "new_lead_notification",
            It.IsAny<List<(string, string)>>(),
            Ct), Times.Once);

        _gmailSender.Verify(x => x.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Test 2: WhatsApp not configured → falls back to email ───────────────

    [Fact]
    public async Task NotifyAsync_WhatsAppNotConfigured_FallsBackToEmail()
    {
        var lead = MakeLead();
        var score = MakeScore();
        var config = MakeConfig(whatsAppPhoneNumberId: null);

        await _notifier.NotifyAsync(lead, score, null, null, config, Ct);

        _whatsAppSender.Verify(x => x.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _gmailSender.Verify(x => x.SendAsync(
            config.AgentId, config.AgentId, config.Email,
            It.IsAny<string>(), It.IsAny<string>(), Ct),
            Times.Once);
    }

    // ─── Test 3: WhatsApp fails → falls back to email ────────────────────────

    [Fact]
    public async Task NotifyAsync_WhatsAppFails_FallsBackToEmail()
    {
        var lead = MakeLead();
        var score = MakeScore();
        var config = MakeConfig();

        _whatsAppSender
            .Setup(x => x.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), Ct))
            .ThrowsAsync(new WhatsAppApiException(131000, "Template not found"));

        await _notifier.NotifyAsync(lead, score, null, null, config, Ct);

        _gmailSender.Verify(x => x.SendAsync(
            config.AgentId, config.AgentId, config.Email,
            It.IsAny<string>(), It.IsAny<string>(), Ct),
            Times.Once);
    }

    // ─── Test 4: WhatsApp + email both fail → logs error, does not throw ─────

    [Fact]
    public async Task NotifyAsync_BothChannelsFail_LogsErrorAndDoesNotThrow()
    {
        var lead = MakeLead();
        var score = MakeScore();
        var config = MakeConfig();

        _whatsAppSender
            .Setup(x => x.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), Ct))
            .ThrowsAsync(new WhatsAppApiException(500, "Internal error"));

        _gmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP failure"));

        var act = async () => await _notifier.NotifyAsync(lead, score, null, null, config, Ct);

        await act.Should().NotThrowAsync();

        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AGENT-NOTIFY-002")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─── Test 5: Message format — contains name, phone, email, timeline, score, bucket ─

    [Fact]
    public async Task NotifyAsync_WhatsApp_TemplateParametersContainLeadContactAndScore()
    {
        var lead = MakeLead();
        var score = MakeScore(75);

        List<(string type, string value)>? capturedParams = null;

        _whatsAppSender
            .Setup(x => x.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, List<(string, string)>, CancellationToken>(
                (_, _, p, _) => capturedParams = p)
            .ReturnsAsync("msg-id");

        var waConfig = MakeConfig(whatsAppPhoneNumberId: "999");
        await _notifier.NotifyAsync(lead, score, null, null, waConfig, Ct);

        capturedParams.Should().NotBeNull();
        var values = capturedParams!.Select(p => p.value).ToList();

        values.Should().Contain(v => v.Contains("Jane Doe"));
        values.Should().Contain(v => v.Contains("555-1234"));
        values.Should().Contain(v => v.Contains("jane@example.com"));
        values.Should().Contain(v => v.Contains("3-6 months"));
        values.Should().Contain(v => v.Contains("75") || v.Contains("Hot"));
    }

    // ─── Test 6: Seller details — contains address, estimated value, comp count ─

    [Fact]
    public async Task NotifyAsync_Email_SellerDetails_ContainsAddressEstimatedValueCompCount()
    {
        var seller = new SellerDetails
        {
            Address = "123 Oak Ave",
            City = "Trenton",
            State = "NJ",
            Zip = "08601",
            Beds = 3,
            Baths = 2,
            AskingPrice = 350_000m
        };
        var lead = MakeLead(seller: seller);
        var score = MakeScore();
        var cmaResult = new CmaWorkerResult(
            LeadId: lead.Id.ToString(),
            Success: true,
            Error: null,
            EstimatedValue: 360_000m,
            PriceRangeLow: 340_000m,
            PriceRangeHigh: 380_000m,
            Comps: new List<CompSummary>
            {
                new("456 Elm St", 355_000m, 3, 2, 1800, 14, 0.3, null),
                new("789 Pine Rd", 365_000m, 4, 2, 2000, 7, 0.5, null)
            },
            MarketAnalysis: "Strong seller's market"
        );
        var config = MakeConfig(whatsAppPhoneNumberId: null);

        string? capturedHtml = null;
        _gmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>(
                (_, _, _, _, html, _) => capturedHtml = html)
            .Returns(Task.CompletedTask);

        await _notifier.NotifyAsync(lead, score, cmaResult, null, config, Ct);

        capturedHtml.Should().NotBeNull();
        capturedHtml.Should().Contain("123 Oak Ave");
        capturedHtml.Should().Contain("360,000");
        capturedHtml.Should().Contain("2"); // 2 comps
    }

    // ─── Test 7: Buyer details — contains area, price range, listing count ────

    [Fact]
    public async Task NotifyAsync_Email_BuyerDetails_ContainsAreaPriceRangeListingCount()
    {
        var buyer = new BuyerDetails
        {
            City = "Princeton",
            State = "NJ",
            MinBudget = 300_000m,
            MaxBudget = 450_000m
        };
        var lead = MakeLead(buyer: buyer);
        var score = MakeScore();
        var homeSearchResult = new HomeSearchWorkerResult(
            LeadId: lead.Id.ToString(),
            Success: true,
            Error: null,
            Listings: new List<ListingSummary>
            {
                new("101 Buyer Ln", 320_000m, 3, 2, 1700, "Active", "http://example.com/1"),
                new("202 Dream St", 400_000m, 4, 3, 2200, "Active", "http://example.com/2"),
                new("303 Hope Ave", 430_000m, 3, 2, 1900, "Active", "http://example.com/3")
            },
            AreaSummary: "Active market with good inventory"
        );
        var config = MakeConfig(whatsAppPhoneNumberId: null);

        string? capturedHtml = null;
        _gmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>(
                (_, _, _, _, html, _) => capturedHtml = html)
            .Returns(Task.CompletedTask);

        await _notifier.NotifyAsync(lead, score, null, homeSearchResult, config, Ct);

        capturedHtml.Should().NotBeNull();
        capturedHtml.Should().Contain("Princeton");
        capturedHtml.Should().Contain("300,000");
        capturedHtml.Should().Contain("450,000");
        capturedHtml.Should().Contain("3"); // 3 listings
    }

    // ─── Test 8: Both seller + buyer → contains both detail sections ─────────

    [Fact]
    public async Task NotifyAsync_Email_BothSellerAndBuyer_ContainsBothDetailSections()
    {
        var seller = new SellerDetails
        {
            Address = "50 Both St",
            City = "Camden",
            State = "NJ",
            Zip = "08101"
        };
        var buyer = new BuyerDetails
        {
            City = "Newark",
            State = "NJ",
            MinBudget = 200_000m,
            MaxBudget = 300_000m
        };

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "jenise-buckalew",
            LeadType = LeadType.Both,
            FirstName = "Alex",
            LastName = "Smith",
            Email = "alex@example.com",
            Phone = "555-9999",
            Timeline = "asap",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = seller,
            BuyerDetails = buyer
        };

        var score = MakeScore(80);
        var config = MakeConfig(whatsAppPhoneNumberId: null);

        string? capturedHtml = null;
        _gmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>(
                (_, _, _, _, html, _) => capturedHtml = html)
            .Returns(Task.CompletedTask);

        await _notifier.NotifyAsync(lead, score, null, null, config, Ct);

        capturedHtml.Should().NotBeNull();
        capturedHtml.Should().Contain("50 Both St"); // seller address
        capturedHtml.Should().Contain("Newark");     // buyer city
    }

    // ─── Test 9: Uses `new_lead_notification` WhatsApp template name ─────────

    [Fact]
    public async Task NotifyAsync_WhatsApp_UsesNewLeadNotificationTemplateName()
    {
        var lead = MakeLead();
        var score = MakeScore();
        var config = MakeConfig();

        _whatsAppSender
            .Setup(x => x.SendTemplateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<(string, string)>>(), Ct))
            .ReturnsAsync("msg-id");

        await _notifier.NotifyAsync(lead, score, null, null, config, Ct);

        _whatsAppSender.Verify(x => x.SendTemplateAsync(
            It.IsAny<string>(),
            "new_lead_notification",
            It.IsAny<List<(string, string)>>(),
            Ct), Times.Once);
    }

    // ─── Test 10: XSS injection — user input is HTML-encoded ─────────────────

    [Fact]
    public async Task NotifyAsync_Email_XssPayloadsInLeadFields_AreHtmlEncoded()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "jenise-buckalew",
            LeadType = LeadType.Seller,
            FirstName = "<script>alert('xss')</script>",
            LastName = "Doe",
            Email = "xss@example.com",
            Phone = "555-0000",
            Timeline = "now",
            Notes = "<img src=x onerror=\"alert('xss')\">",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = new SellerDetails
            {
                Address = "<script>evil()</script>",
                City = "<b>BadCity",
                State = "NJ",
                Zip = "00000"
            }
        };
        var score = MakeScore();
        var cmaResult = new CmaWorkerResult(
            LeadId: lead.Id.ToString(),
            Success: true,
            Error: null,
            EstimatedValue: 300_000m,
            PriceRangeLow: 280_000m,
            PriceRangeHigh: 320_000m,
            Comps: [],
            MarketAnalysis: "<script>market()</script>"
        );
        var config = MakeConfig(whatsAppPhoneNumberId: null);

        string? capturedHtml = null;
        _gmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>(
                (_, _, _, _, html, _) => capturedHtml = html)
            .Returns(Task.CompletedTask);

        await _notifier.NotifyAsync(lead, score, cmaResult, null, config, Ct);

        capturedHtml.Should().NotBeNull();
        capturedHtml.Should().NotContain("<script>");
        capturedHtml.Should().Contain("&lt;script&gt;");
        capturedHtml.Should().NotContain("<img src=x");
        capturedHtml.Should().Contain("&lt;img src=x");
    }

    // ─── Test 11: Fallback email uses HTML formatting with agent branding ─────

    [Fact]
    public async Task NotifyAsync_FallbackEmail_UsesHtmlFormattingWithAgentBranding()
    {
        var lead = MakeLead();
        var score = MakeScore();
        var config = MakeConfig(whatsAppPhoneNumberId: null);

        string? capturedHtml = null;
        _gmailSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>(
                (_, _, _, _, html, _) => capturedHtml = html)
            .Returns(Task.CompletedTask);

        await _notifier.NotifyAsync(lead, score, null, null, config, Ct);

        capturedHtml.Should().NotBeNull();
        capturedHtml.Should().Contain("<html");
        capturedHtml.Should().Contain(config.PrimaryColor);
        capturedHtml.Should().Contain("Jenise Buckalew");
        capturedHtml.Should().Contain("Keller Williams");
    }
}
