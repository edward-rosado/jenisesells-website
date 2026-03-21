using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Features.Leads.Submit;

namespace RealEstateStar.Api.Tests.Features.Leads.Services.Enrichment;

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

    /// <summary>Deserializes the Claude API request body and extracts the user message content string.</summary>
    private static string ExtractMessageContent(string requestBodyJson)
    {
        using var doc = JsonDocument.Parse(requestBodyJson);
        return doc.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString() ?? "";
    }

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

    private static string WrapInClaudeApiResponse(string analysisJson, int inputTokens = 100, int outputTokens = 200)
    {
        var serializedText = JsonSerializer.Serialize(analysisJson);
        return "{\"content\":[{\"text\":" + serializedText + "}],\"usage\":{\"input_tokens\":" + inputTokens + ",\"output_tokens\":" + outputTokens + "}}";
    }

    // ---------------------------------------------------------------------------
    // Factory helpers
    // ---------------------------------------------------------------------------

    /// <summary>Creates a factory where ALL requests return the same response.</summary>
    private static (ScraperLeadEnricher enricher, Mock<HttpMessageHandler> handler) CreateEnricher(
        HttpStatusCode status, string content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = status, Content = new StringContent(content) });

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var enricher = new ScraperLeadEnricher(
            factory.Object,
            "test-claude-key",
            "test-scraper-key",
            NullLogger<ScraperLeadEnricher>.Instance);

        return (enricher, handler);
    }

    /// <summary>Creates a factory with a custom per-request callback for capturing calls.</summary>
    private static (ScraperLeadEnricher enricher, List<HttpRequestMessage> capturedRequests) CreateCapturingEnricher(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                lock (captured) captured.Add(req);
                return responseFactory(req, ct);
            });

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var enricher = new ScraperLeadEnricher(
            factory.Object,
            "test-claude-key",
            "test-scraper-key",
            NullLogger<ScraperLeadEnricher>.Instance);

        return (enricher, captured);
    }

    // ---------------------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsParsedEnrichmentAndScore_WhenClaudeResponds()
    {
        var claudeJson = MakeValidClaudeJson(motivationCategory: "Relocating", overallScore: 75);
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        // Scrapers return empty, Claude returns valid response
        var (enricher, _) = CreateCapturingEnricher((req, _) =>
        {
            // Claude API requests (POST to anthropic) return analysis JSON
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };
            // Scraper requests return empty content
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

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
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        var (enricher, capturedRequests) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("scraped content") };
        });

        var lead = MakeLead();
        await enricher.EnrichAsync(lead, CancellationToken.None);

        // 8 scraper GET requests + 1 Claude POST request = 9 total
        var getRequests = capturedRequests.Where(r => r.Method == HttpMethod.Get).ToList();
        getRequests.Should().HaveCount(8, "should fire all 8 scraping sources");
    }

    [Fact]
    public async Task EnrichAsync_IncludesScraperApiKeyInUrls()
    {
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        var (enricher, capturedRequests) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("scraped") };
        });

        var lead = MakeLead();
        await enricher.EnrichAsync(lead, CancellationToken.None);

        var getRequests = capturedRequests.Where(r => r.Method == HttpMethod.Get).ToList();
        getRequests.Should().AllSatisfy(r =>
            r.RequestUri!.ToString().Should().Contain("api_key=test-scraper-key"));
    }

    // ---------------------------------------------------------------------------
    // Partial failures (some sources timeout)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_HandlesPartialScraperFailures_StillCallsClaude()
    {
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        var callCount = 0;
        var (enricher, capturedRequests) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };

            // Half of scraper requests fail
            var count = Interlocked.Increment(ref callCount);
            if (count % 2 == 0)
                throw new HttpRequestException("Connection refused");

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("scraped content") };
        });

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        // Should still return valid enrichment from Claude
        enrichment.MotivationCategory.Should().NotBeNullOrEmpty();
        score.OverallScore.Should().BeGreaterThan(0);

        // Claude was called exactly once (POST)
        capturedRequests.Count(r => r.Method == HttpMethod.Post).Should().Be(1);
    }

    [Fact]
    public async Task EnrichAsync_WhenAllScrapersFail_StillCallsClaudeWithMinimalData()
    {
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        var (enricher, capturedRequests) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };

            throw new HttpRequestException("All scrapers down");
        });

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.MotivationCategory.Should().Be("Relocating");

        // Claude was still called
        capturedRequests.Count(r => r.Method == HttpMethod.Post).Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // Graceful degradation — Claude unavailable
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsEmptyAndDefault_WhenClaudeReturnsError()
    {
        var (enricher, _) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error")
                };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.MotivationCategory.Should().Be("unknown");
        enrichment.ConversationStarters.Should().BeEmpty();
        score.OverallScore.Should().Be(50);
        score.Explanation.Should().Be("enrichment unavailable");
    }

    [Fact]
    public async Task EnrichAsync_ReturnsEmptyAndDefault_WhenClaudeThrows()
    {
        var (enricher, _) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
                throw new HttpRequestException("Network error");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.Should().BeEquivalentTo(LeadEnrichment.Empty());
        score.Explanation.Should().Be("enrichment unavailable");
    }

    // ---------------------------------------------------------------------------
    // Markdown code fence stripping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_StripsMarkdownCodeFences_BeforeParsing()
    {
        var rawJson = MakeValidClaudeJson();
        var fencedJson = $"```json\n{rawJson}\n```";
        var claudeApiResponse = WrapInClaudeApiResponse(fencedJson);

        var (enricher, _) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        var lead = MakeLead();
        var (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);

        enrichment.MotivationCategory.Should().Be("Relocating");
        score.OverallScore.Should().Be(75);
    }

    [Theory]
    [InlineData("```json\n{}\n```")]
    [InlineData("```\n{}\n```")]
    public void StripCodeFences_RemovesFenceMarkers(string input)
    {
        var result = ScraperLeadEnricher.StripCodeFences(input);
        result.Should().Be("{}");
    }

    [Fact]
    public void StripCodeFences_ReturnsUnchanged_WhenNoFences()
    {
        var json = """{"key": "value"}""";
        var result = ScraperLeadEnricher.StripCodeFences(json);
        result.Should().Be(json);
    }

    // ---------------------------------------------------------------------------
    // Token usage logging
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_LogsTokenUsageWithLead026Code()
    {
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson, inputTokens: 500, outputTokens: 300);

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

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                req.Method == HttpMethod.Post
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var enricher = new ScraperLeadEnricher(factory.Object, "key", "scraper-key", mockLogger.Object);
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
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        string? capturedRequestBody = null;
        var (enricher, capturedRequests) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
            {
                // Synchronously capture the body (we can't await in a sync lambda, so buffer)
                capturedRequestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("scraped data for linkedin") };
        });

        var lead = MakeLead();
        await enricher.EnrichAsync(lead, CancellationToken.None);

        capturedRequestBody.Should().NotBeNull();
        var messageContent = ExtractMessageContent(capturedRequestBody!);
        messageContent.Should().Contain("<source name=\"linkedin\">");
        messageContent.Should().Contain("</source>");
        messageContent.Should().Contain("<lead_data>");
        messageContent.Should().Contain("</lead_data>");
    }

    [Fact]
    public async Task EnrichAsync_EscapesXmlSpecialChars_InLeadData()
    {
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        string? capturedRequestBody = null;
        var (enricher, _) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
            {
                capturedRequestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        // Lead with XML-injection-attempt name
        var lead = MakeLead(firstName: "<script>", lastName: "alert('xss')</script>");
        await enricher.EnrichAsync(lead, CancellationToken.None);

        capturedRequestBody.Should().NotBeNull();
        var messageContent = ExtractMessageContent(capturedRequestBody!);
        messageContent.Should().NotContain("<script>");
        messageContent.Should().Contain("&lt;script&gt;");
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
        var claudeJson = MakeValidClaudeJson();
        var claudeApiResponse = WrapInClaudeApiResponse(claudeJson);

        string? capturedRequestBody = null;
        var (enricher, _) = CreateCapturingEnricher((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
            {
                capturedRequestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(claudeApiResponse) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        var buyerLead = MakeLead(
            omitSellerDetails: true,
            buyerDetails: new BuyerDetails { City = "Princeton", State = "NJ", MaxBudget = 750_000 });

        await enricher.EnrichAsync(buyerLead, CancellationToken.None);

        capturedRequestBody.Should().NotBeNull();
        var messageContent = ExtractMessageContent(capturedRequestBody!);
        messageContent.Should().Contain("Princeton");
        messageContent.Should().Contain("NJ");
    }
}
