using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Gws;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class MultiChannelLeadNotifierTests
{
    // ─── MockHttpMessageHandler ────────────────────────────────────────────────

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public HttpResponseMessage Response { get; set; } = new(System.Net.HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(Response);
        }
    }

    // ─── Shared test data ──────────────────────────────────────────────────────

    private static Lead MakeLead() => new()
    {
        Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
        AgentId = "jenise-buckalew",
        LeadTypes = ["buying", "selling"],
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "5551234567",
        Timeline = "1-3months",
        ReceivedAt = new DateTime(2026, 3, 19, 14, 30, 0, DateTimeKind.Utc),
        Status = LeadStatus.Enriched,
        SellerDetails = new SellerDetails
        {
            Address = "123 Main St",
            City = "Springfield",
            State = "NJ",
            Zip = "07081"
        },
        BuyerDetails = new BuyerDetails
        {
            City = "Kill Devil Hills",
            State = "NC"
        }
    };

    private static LeadEnrichment MakeEnrichment() => new()
    {
        MotivationCategory = "relocating",
        MotivationAnalysis = "Relocating for a new job opportunity.",
        ProfessionalBackground = "Software engineer, stable income.",
        FinancialIndicators = "Pre-approved for $500k.",
        TimelinePressure = "Needs to move within 90 days.",
        ConversationStarters = ["Ask about the new role."],
        ColdCallOpeners = ["Congratulations on the new opportunity!", "I specialize in relocation buyers."]
    };

    private static LeadScore MakeScore() => new()
    {
        OverallScore = 82,
        Factors = [],
        Explanation = "Strong motivation and timeline."
    };

    private static AgentConfig MakeConfig(string? chatWebhookUrl = null) => new()
    {
        Id = "jenise-buckalew",
        Identity = new AgentIdentity
        {
            Name = "Jenise Buckalew",
            Email = "jenise@example.com",
            Phone = "(973) 555-0100"
        },
        Integrations = new AgentIntegrations
        {
            ChatWebhookUrl = chatWebhookUrl
        }
    };

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyAgentAsync_WhenWebhookConfigured_SendsGoogleChatRequest()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("GoogleChat")).Returns(httpClient);

        var gwsService = new Mock<IGwsService>();
        var configService = new Mock<IAgentConfigService>();
        configService.Setup(c => c.GetAgentAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(chatWebhookUrl: "https://chat.googleapis.com/webhook/test"));

        var sut = new MultiChannelLeadNotifier(httpFactory.Object, gwsService.Object, configService.Object,
            new Mock<ILogger<MultiChannelLeadNotifier>>().Object);

        await sut.NotifyAgentAsync("jenise-buckalew", MakeLead(), MakeEnrichment(), MakeScore(), CancellationToken.None);

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].RequestUri!.ToString().Should().Be("https://chat.googleapis.com/webhook/test");
    }

    [Fact]
    public async Task NotifyAgentAsync_WhenWebhookNotConfigured_SkipsGoogleChat()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("GoogleChat")).Returns(httpClient);

        var gwsService = new Mock<IGwsService>();
        var configService = new Mock<IAgentConfigService>();
        configService.Setup(c => c.GetAgentAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(chatWebhookUrl: null));

        var sut = new MultiChannelLeadNotifier(httpFactory.Object, gwsService.Object, configService.Object,
            new Mock<ILogger<MultiChannelLeadNotifier>>().Object);

        await sut.NotifyAgentAsync("jenise-buckalew", MakeLead(), MakeEnrichment(), MakeScore(), CancellationToken.None);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAgentAsync_AlwaysSendsEmail()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        var gwsService = new Mock<IGwsService>();
        var configService = new Mock<IAgentConfigService>();
        configService.Setup(c => c.GetAgentAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var sut = new MultiChannelLeadNotifier(httpFactory.Object, gwsService.Object, configService.Object,
            new Mock<ILogger<MultiChannelLeadNotifier>>().Object);

        await sut.NotifyAgentAsync("jenise-buckalew", MakeLead(), MakeEnrichment(), MakeScore(), CancellationToken.None);

        gwsService.Verify(g => g.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAgentAsync_WhenChatFails_LogsWarningAndContinuesToEmail()
    {
        var handler = new MockHttpMessageHandler
        {
            Response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        };
        var httpClient = new HttpClient(handler);
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("GoogleChat")).Returns(httpClient);

        var gwsService = new Mock<IGwsService>();
        var configService = new Mock<IAgentConfigService>();
        configService.Setup(c => c.GetAgentAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(chatWebhookUrl: "https://chat.googleapis.com/webhook/test"));

        var logger = new Mock<ILogger<MultiChannelLeadNotifier>>();
        var sut = new MultiChannelLeadNotifier(httpFactory.Object, gwsService.Object, configService.Object, logger.Object);

        // Should NOT throw
        var act = async () => await sut.NotifyAgentAsync("jenise-buckalew", MakeLead(), MakeEnrichment(), MakeScore(), CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Should log warning with [LEAD-033]
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("LEAD-033")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

        // Should still send email
        gwsService.Verify(g => g.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAgentAsync_WhenEmailFails_LogsErrorButDoesNotThrow()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        var gwsService = new Mock<IGwsService>();
        gwsService.Setup(g => g.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("email send failed"));

        var configService = new Mock<IAgentConfigService>();
        configService.Setup(c => c.GetAgentAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var logger = new Mock<ILogger<MultiChannelLeadNotifier>>();
        var sut = new MultiChannelLeadNotifier(httpFactory.Object, gwsService.Object, configService.Object, logger.Object);

        // Should NOT throw
        var act = async () => await sut.NotifyAgentAsync("jenise-buckalew", MakeLead(), MakeEnrichment(), MakeScore(), CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Should log error with [LEAD-034]
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("LEAD-034")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAgentAsync_BothChannelsFire_WhenBothConfigured()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("GoogleChat")).Returns(httpClient);

        var gwsService = new Mock<IGwsService>();
        var configService = new Mock<IAgentConfigService>();
        configService.Setup(c => c.GetAgentAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(chatWebhookUrl: "https://chat.googleapis.com/webhook/test"));

        var sut = new MultiChannelLeadNotifier(httpFactory.Object, gwsService.Object, configService.Object,
            new Mock<ILogger<MultiChannelLeadNotifier>>().Object);

        await sut.NotifyAgentAsync("jenise-buckalew", MakeLead(), MakeEnrichment(), MakeScore(), CancellationToken.None);

        handler.Requests.Should().HaveCount(1);
        gwsService.Verify(g => g.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── BuildSubject tests ───────────────────────────────────────────────────

    [Fact]
    public void BuildSubject_IncludesMotivationCategory()
    {
        var subject = MultiChannelLeadNotifier.BuildSubject(MakeLead(), MakeEnrichment(), MakeScore());
        subject.Should().Contain("relocating");
    }

    [Fact]
    public void BuildSubject_IncludesScore()
    {
        var subject = MultiChannelLeadNotifier.BuildSubject(MakeLead(), MakeEnrichment(), MakeScore());
        subject.Should().Contain("82");
    }

    [Fact]
    public void BuildSubject_IncludesLeadName()
    {
        var subject = MultiChannelLeadNotifier.BuildSubject(MakeLead(), MakeEnrichment(), MakeScore());
        subject.Should().Contain("Jane Doe");
    }

    // ─── BuildEmailBody tests ─────────────────────────────────────────────────

    [Fact]
    public void BuildEmailBody_IncludesColdCallOpeners()
    {
        var body = MultiChannelLeadNotifier.BuildEmailBody(MakeLead(), MakeEnrichment(), MakeScore());
        body.Should().Contain("Cold Call Openers");
        body.Should().Contain("Congratulations on the new opportunity!");
    }

    [Fact]
    public void BuildEmailBody_SellerOnlyLead_OmitsBuyerSection()
    {
        var lead = new Lead
        {
            Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            AgentId = "jenise-buckalew",
            LeadTypes = ["selling"],
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "5551234567",
            Timeline = "1-3months",
            ReceivedAt = new DateTime(2026, 3, 19, 14, 30, 0, DateTimeKind.Utc),
            Status = LeadStatus.Enriched,
            SellerDetails = new SellerDetails { Address = "123 Main St", City = "Springfield", State = "NJ", Zip = "07081" }
        };
        var body = MultiChannelLeadNotifier.BuildEmailBody(lead, MakeEnrichment(), MakeScore());
        body.Should().Contain("## Selling");
        body.Should().NotContain("## Buying");
    }

    [Fact]
    public void BuildEmailBody_BuyerOnlyLead_OmitsSellerSection()
    {
        var lead = new Lead
        {
            Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            AgentId = "jenise-buckalew",
            LeadTypes = ["buying"],
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "5551234567",
            Timeline = "1-3months",
            ReceivedAt = new DateTime(2026, 3, 19, 14, 30, 0, DateTimeKind.Utc),
            Status = LeadStatus.Enriched,
            BuyerDetails = new BuyerDetails { City = "Kill Devil Hills", State = "NC" }
        };
        var body = MultiChannelLeadNotifier.BuildEmailBody(lead, MakeEnrichment(), MakeScore());
        body.Should().Contain("## Buying");
        body.Should().NotContain("## Selling");
    }

    [Fact]
    public void BuildEmailBody_IncludesEnrichmentSummary()
    {
        var body = MultiChannelLeadNotifier.BuildEmailBody(MakeLead(), MakeEnrichment(), MakeScore());
        body.Should().Contain("Enrichment Summary");
        body.Should().Contain("Relocating for a new job opportunity.");
    }

    // ─── BuildEmailBody — optional seller fields ──────────────────────────────

    [Fact]
    public void BuildEmailBody_SellerWithOptionalFields_IncludesPropertyTypeConditionAndAskingPrice()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "jenise-buckalew",
            LeadTypes = ["selling"],
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "5551234567",
            Timeline = "ASAP",
            ReceivedAt = DateTime.UtcNow,
            Status = LeadStatus.Received,
            SellerDetails = new SellerDetails
            {
                Address = "123 Main St",
                City = "Springfield",
                State = "NJ",
                Zip = "07081",
                PropertyType = "Single Family",
                Condition = "Good",
                AskingPrice = 450_000m
            }
        };

        var body = MultiChannelLeadNotifier.BuildEmailBody(lead, MakeEnrichment(), MakeScore());

        body.Should().Contain("Single Family");
        body.Should().Contain("Good");
        body.Should().Contain("450,000");
    }

    // ─── BuildEmailBody — optional buyer fields ───────────────────────────────

    [Fact]
    public void BuildEmailBody_BuyerWithOptionalFields_IncludesMaxBudgetBedroomsBathroomsPropertyTypesAndMustHaves()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "jenise-buckalew",
            LeadTypes = ["buying"],
            FirstName = "Bob",
            LastName = "Smith",
            Email = "bob@example.com",
            Phone = "5559876543",
            Timeline = "1-3 months",
            ReceivedAt = DateTime.UtcNow,
            Status = LeadStatus.Received,
            BuyerDetails = new BuyerDetails
            {
                City = "Springfield",
                State = "NJ",
                MaxBudget = 500_000m,
                Bedrooms = 3,
                Bathrooms = 2,
                PropertyTypes = ["Single Family", "Condo"],
                MustHaves = ["Garage", "Backyard"]
            }
        };

        var body = MultiChannelLeadNotifier.BuildEmailBody(lead, MakeEnrichment(), MakeScore());

        body.Should().Contain("500,000");
        body.Should().Contain("3");        // bedrooms
        body.Should().Contain("2");        // bathrooms
        body.Should().Contain("Single Family");
        body.Should().Contain("Garage");
    }

    [Fact]
    public void BuildEmailBody_WhenNoColdCallOpeners_OmitsColdCallOpenersSection()
    {
        var enrichment = MakeEnrichment() with { ColdCallOpeners = [] };

        var body = MultiChannelLeadNotifier.BuildEmailBody(MakeLead(), enrichment, MakeScore());

        body.Should().NotContain("Cold Call Openers");
    }
}
