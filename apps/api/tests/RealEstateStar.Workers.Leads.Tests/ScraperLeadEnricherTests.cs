using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Leads;

namespace RealEstateStar.Workers.Leads.Tests;

public class ScraperLeadEnricherTests
{
    private static readonly SellerDetails DefaultSellerDetails = new()
    {
        Address = "123 Main St",
        City = "Old Bridge",
        State = "NJ",
        Zip = "08857",
    };

    private static Lead MakeLead(
        string firstName = "Jane",
        string lastName = "Doe",
        string email = "jane@example.com",
        string phone = "555-1234",
        string timeline = "3-6 months",
        string? notes = null,
        SellerDetails? sellerDetails = null,
        BuyerDetails? buyerDetails = null,
        bool omitSellerDetails = false) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "test-agent",
        LeadType = LeadType.Seller,
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Phone = phone,
        Timeline = timeline,
        Notes = notes,
        SellerDetails = omitSellerDetails ? null : sellerDetails ?? DefaultSellerDetails,
        BuyerDetails = buyerDetails,
    };

    private static string MakeValidClaudeJson(
        string motivationCategory = "Relocating",
        int overallScore = 75) => $$"""
        {
            "motivationCategory": "{{motivationCategory}}",
            "motivationAnalysis": "Lead appears to be relocating due to job change.",
            "professionalBackground": "Software Engineer at Acme Corp",
            "financialIndicators": "Likely has equity based on purchase date.",
            "timelinePressure": "Moderate — 3-6 month window",
            "conversationStarters": ["Ask about their timeline", "Mention recent sales nearby"],
            "coldCallOpeners": ["Hi, I saw you might be considering selling...", "I specialize in your neighborhood..."],
            "overallScore": {{overallScore}},
            "scoreFactors": [
                {
                    "category": "Timeline",
                    "score": 80,
                    "weight": 0.3,
                    "explanation": "Has a defined timeline of 3-6 months"
                }
            ],
            "scoreExplanation": "Good prospect with clear motivation and defined timeline."
        }
        """;

    // ---------------------------------------------------------------------------
    // Factory helpers
    // ---------------------------------------------------------------------------

    private static (ScraperLeadEnricher enricher, Mock<IAnthropicClient> anthropicClient, Mock<IScraperClient> scraperClient)
        CreateEnricher(
            AnthropicResponse? anthropicResponse = null,
            Exception? anthropicException = null,
            Func<string, string?, Task<string?>>? scraperResponseFactory = null)
    {
        var anthropicClient = new Mock<IAnthropicClient>();

        if (anthropicException is not null)
        {
            anthropicClient.Setup(c => c.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(anthropicException);
        }
        else
        {
            var response = anthropicResponse ?? new AnthropicResponse(MakeValidClaudeJson(), 100, 200, 500);
            anthropicClient.Setup(c => c.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
        }

        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);

        if (scraperResponseFactory is not null)
        {
            scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, string, CancellationToken>((url, source, agentId, _) =>
                    scraperResponseFactory(url, source));
        }
        else
        {
            scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);
        }

        var sourceUrls = new Dictionary<string, string>
        {
            ["google"] = "https://www.google.com/search?q={query}"
        };

        var enricher = new ScraperLeadEnricher(
            anthropicClient.Object,
            scraperClient.Object,
            sourceUrls,
            NullLogger<ScraperLeadEnricher>.Instance);

        return (enricher, anthropicClient, scraperClient);
    }

    // ---------------------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsParsedEnrichmentAndScore_WhenClaudeResponds()
    {
        var json = MakeValidClaudeJson(motivationCategory: "Relocating", overallScore: 75);
        var (enricher, _, _) = CreateEnricher(new AnthropicResponse(json, 100, 200, 500));

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.MotivationCategory.Should().Be("Relocating");
        enrichment.MotivationAnalysis.Should().Contain("relocating");
        enrichment.ConversationStarters.Should().HaveCount(2);
        enrichment.ColdCallOpeners.Should().HaveCount(2);
        score.OverallScore.Should().Be(75);
        score.Factors.Should().HaveCount(1);
        score.Explanation.Should().Contain("prospect");
    }

    // ---------------------------------------------------------------------------
    // Parallel scraping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_FiresAllEightScrapingQueriesInParallel()
    {
        var (enricher, _, scraperClient) = CreateEnricher(
            scraperResponseFactory: (_, _) => Task.FromResult<string?>("scraped content"));

        var lead = MakeLead();
        await enricher.EnrichAsync(lead, CancellationToken.None);

        // Scraper client should have been called for all 8 sources
        scraperClient.Verify(
            s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(8));
    }

    // ---------------------------------------------------------------------------
    // Partial failures (some sources timeout)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_HandlesPartialScraperFailures_StillCallsClaude()
    {
        var callCount = 0;
        var (enricher, anthropicClient, _) = CreateEnricher(
            scraperResponseFactory: (_, _) =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count % 2 == 0)
                    throw new HttpRequestException("Connection refused");
                return Task.FromResult<string?>("scraped content");
            });

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.MotivationCategory.Should().NotBeNullOrEmpty();
        score.OverallScore.Should().BeGreaterThan(0);

        // Claude was called exactly once
        anthropicClient.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnrichAsync_WhenAllScrapersFail_StillCallsClaudeWithMinimalData()
    {
        var (enricher, anthropicClient, _) = CreateEnricher(
            scraperResponseFactory: (_, _) =>
                throw new HttpRequestException("All scrapers down"));

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.MotivationCategory.Should().Be("Relocating");

        // Claude was still called
        anthropicClient.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Graceful degradation — Claude unavailable
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsEmptyAndDefault_WhenClaudeThrows()
    {
        var (enricher, _, _) = CreateEnricher(
            anthropicException: new HttpRequestException("Network error"));

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.Should().BeEquivalentTo(LeadEnrichment.Empty());
        score.Explanation.Should().Be("enrichment unavailable");
    }

    [Fact]
    public async Task EnrichAsync_ReturnsEmptyAndDefault_WhenClaudeReturnsInvalidJson()
    {
        var (enricher, _, _) = CreateEnricher(new AnthropicResponse("not valid json", 10, 5, 100));

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.MotivationCategory.Should().Be("unknown");
        enrichment.ConversationStarters.Should().BeEmpty();
        score.OverallScore.Should().Be(50);
        score.Explanation.Should().Be("enrichment unavailable");
    }

    // ---------------------------------------------------------------------------
    // Token usage logging
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_LogsTokenUsageWithLead026Code()
    {
        var json = MakeValidClaudeJson();
        var logMessages = new List<string>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ScraperLeadEnricher>>();
        mockLogger
            .Setup(l => l.Log(
                It.IsAny<Microsoft.Extensions.Logging.LogLevel>(),
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<Microsoft.Extensions.Logging.LogLevel, Microsoft.Extensions.Logging.EventId, object, Exception?, Delegate>(
                (_, _, state, _, formatter) =>
                {
                    var msg = formatter.DynamicInvoke(state, null)?.ToString();
                    if (msg is not null) logMessages.Add(msg);
                });

        var anthropicClient = new Mock<IAnthropicClient>();
        anthropicClient.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse(json, 500, 300, 1000));

        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var enricher = new ScraperLeadEnricher(
            anthropicClient.Object,
            scraperClient.Object,
            new Dictionary<string, string> { ["google"] = "https://www.google.com/search?q={query}" },
            mockLogger.Object);

        var lead = MakeLead();
        await enricher.EnrichAsync(lead, CancellationToken.None);

        logMessages.Should().Contain(m => m.Contains("[LEAD-026]") && m.Contains("500") && m.Contains("300"));
    }

    // ---------------------------------------------------------------------------
    // XML wrapping for prompt injection prevention
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_XmlWrapsSourceData_InUserMessage()
    {
        string? capturedUserMessage = null;
        var anthropicClient = new Mock<IAnthropicClient>();
        anthropicClient.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(new AnthropicResponse(MakeValidClaudeJson(), 100, 200, 500));

        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string, CancellationToken>((_, source, _, _) =>
                Task.FromResult<string?>("scraped data for " + source));

        var enricher = new ScraperLeadEnricher(
            anthropicClient.Object,
            scraperClient.Object,
            new Dictionary<string, string> { ["google"] = "https://www.google.com/search?q={query}" },
            NullLogger<ScraperLeadEnricher>.Instance);

        var lead = MakeLead();
        await enricher.EnrichAsync(lead, CancellationToken.None);

        capturedUserMessage.Should().NotBeNull();
        capturedUserMessage.Should().Contain("<source name=\"google:linkedin\">");
        capturedUserMessage.Should().Contain("</source>");
        capturedUserMessage.Should().Contain("<lead_data>");
        capturedUserMessage.Should().Contain("</lead_data>");
    }

    [Fact]
    public async Task EnrichAsync_EscapesXmlSpecialChars_InLeadData()
    {
        string? capturedUserMessage = null;
        var anthropicClient = new Mock<IAnthropicClient>();
        anthropicClient.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(new AnthropicResponse(MakeValidClaudeJson(), 100, 200, 500));

        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var enricher = new ScraperLeadEnricher(
            anthropicClient.Object,
            scraperClient.Object,
            new Dictionary<string, string> { ["google"] = "https://www.google.com/search?q={query}" },
            NullLogger<ScraperLeadEnricher>.Instance);

        // Lead with XML-injection-attempt name
        var lead = MakeLead(firstName: "<script>", lastName: "alert('xss')</script>");
        await enricher.EnrichAsync(lead, CancellationToken.None);

        capturedUserMessage.Should().NotBeNull();
        capturedUserMessage.Should().NotContain("<script>");
        capturedUserMessage.Should().Contain("&lt;script&gt;");
    }

    // ---------------------------------------------------------------------------
    // ParseClaudeResponse unit tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseClaudeResponse_ExtractsAllFields()
    {
        var json = MakeValidClaudeJson("Downsizing", 80);
        var (enrichment, score) = ScraperLeadEnricher.ParseClaudeResponse(json);

        enrichment.MotivationCategory.Should().Be("Downsizing");
        enrichment.MotivationAnalysis.Should().NotBeNullOrWhiteSpace();
        enrichment.ProfessionalBackground.Should().NotBeNullOrWhiteSpace();
        enrichment.FinancialIndicators.Should().NotBeNullOrWhiteSpace();
        enrichment.TimelinePressure.Should().NotBeNullOrWhiteSpace();
        enrichment.ConversationStarters.Should().HaveCount(2);
        enrichment.ColdCallOpeners.Should().HaveCount(2);

        score.OverallScore.Should().Be(80);
        score.Factors.Should().HaveCount(1);
        score.Factors[0].Category.Should().Be("Timeline");
        score.Factors[0].Score.Should().Be(80);
        score.Factors[0].Weight.Should().Be(0.3m);
        score.Explanation.Should().Contain("prospect");
    }

    [Fact]
    public void ParseClaudeResponse_HandlesNullListItems_InConversationStarters()
    {
        var json = """
        {
            "motivationCategory": "Unknown",
            "motivationAnalysis": "unknown",
            "professionalBackground": "unknown",
            "financialIndicators": "unknown",
            "timelinePressure": "unknown",
            "conversationStarters": ["valid", null, "also valid"],
            "coldCallOpeners": [],
            "overallScore": 50,
            "scoreFactors": [],
            "scoreExplanation": "default"
        }
        """;

        var (enrichment, _) = ScraperLeadEnricher.ParseClaudeResponse(json);

        enrichment.ConversationStarters.Should().HaveCount(2);
        enrichment.ConversationStarters.Should().NotContainNulls();
    }

    [Fact]
    public void ParseClaudeResponse_DefaultsToFifty_WhenOverallScoreMissing()
    {
        var json = """
        {
            "motivationCategory": "unknown",
            "motivationAnalysis": "unknown",
            "professionalBackground": "unknown",
            "financialIndicators": "unknown",
            "timelinePressure": "unknown",
            "conversationStarters": [],
            "coldCallOpeners": [],
            "scoreFactors": [],
            "scoreExplanation": "no data"
        }
        """;

        var (_, score) = ScraperLeadEnricher.ParseClaudeResponse(json);
        score.OverallScore.Should().Be(50);
    }

    [Fact]
    public void ParseClaudeResponse_DefaultsToUnknown_WhenFieldsMissing()
    {
        var json = """
        {
            "conversationStarters": [],
            "coldCallOpeners": [],
            "overallScore": 50,
            "scoreFactors": [],
            "scoreExplanation": "no data"
        }
        """;

        var (enrichment, _) = ScraperLeadEnricher.ParseClaudeResponse(json);

        enrichment.MotivationCategory.Should().Be("unknown");
        enrichment.MotivationAnalysis.Should().Be("unknown");
        enrichment.ProfessionalBackground.Should().Be("unknown");
    }

    // ---------------------------------------------------------------------------
    // BuyerDetails path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_UsesLocationFromBuyerDetails_WhenNoSellerDetails()
    {
        string? capturedUserMessage = null;
        var anthropicClient = new Mock<IAnthropicClient>();
        anthropicClient.Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(new AnthropicResponse(MakeValidClaudeJson(), 100, 200, 500));

        var scraperClient = new Mock<IScraperClient>();
        scraperClient.Setup(s => s.IsAvailable).Returns(true);
        scraperClient.Setup(s => s.FetchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var enricher = new ScraperLeadEnricher(
            anthropicClient.Object,
            scraperClient.Object,
            new Dictionary<string, string> { ["google"] = "https://www.google.com/search?q={query}" },
            NullLogger<ScraperLeadEnricher>.Instance);

        var buyerLead = MakeLead(
            omitSellerDetails: true,
            buyerDetails: new BuyerDetails { City = "Princeton", State = "NJ", MaxBudget = 750_000 });

        await enricher.EnrichAsync(buyerLead, CancellationToken.None);

        capturedUserMessage.Should().NotBeNull();
        capturedUserMessage.Should().Contain("Princeton");
        capturedUserMessage.Should().Contain("NJ");
    }
}
