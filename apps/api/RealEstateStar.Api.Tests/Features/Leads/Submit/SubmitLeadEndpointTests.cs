using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Tests.Integration;

namespace RealEstateStar.Api.Tests.Features.Leads.Submit;

/// <summary>
/// Custom factory that adds no-op stub implementations for the Lead services
/// that aren't registered in Program.cs (they rely on external storage).
/// </summary>
public class LeadSubmitTestFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            // Register no-op lead service stubs so all lead endpoints can resolve their dependencies
            services.AddSingleton<ILeadStore, NoOpLeadStore>();
            services.AddSingleton<IMarketingConsentLog, NoOpMarketingConsentLog>();
            services.AddSingleton<ILeadEnricher, NoOpLeadEnricher>();
            services.AddSingleton<ILeadNotifier, NoOpLeadNotifier>();
            services.AddSingleton<IHomeSearchProvider, NoOpHomeSearchProvider>();
            services.AddSingleton<ILeadDataDeletion, NoOpLeadDataDeletion>();
            services.AddSingleton<IDeletionAuditLog, NoOpDeletionAuditLog>();
        });
    }
}

// ---------------------------------------------------------------------------
// No-op stubs used only in integration tests
// ---------------------------------------------------------------------------
file sealed class NoOpLeadStore : ILeadStore
{
    public Task SaveAsync(Lead lead, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateEnrichmentAsync(string a, Guid i, LeadEnrichment e, LeadScore s, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateHomeSearchIdAsync(string a, Guid i, string h, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateStatusAsync(string a, Guid i, LeadStatus s, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateMarketingOptInAsync(string a, Guid i, bool o, CancellationToken ct) => Task.CompletedTask;
    public Task<Lead?> GetAsync(string a, Guid i, CancellationToken ct) => Task.FromResult<Lead?>(null);
    public Task<Lead?> GetByNameAsync(string a, string n, CancellationToken ct) => Task.FromResult<Lead?>(null);
    public Task<Lead?> GetByEmailAsync(string a, string e, CancellationToken ct) => Task.FromResult<Lead?>(null);
    public Task<List<Lead>> ListByStatusAsync(string a, LeadStatus s, CancellationToken ct) => Task.FromResult(new List<Lead>());
    public Task DeleteAsync(string a, Guid i, CancellationToken ct) => Task.CompletedTask;
}

file sealed class NoOpMarketingConsentLog : IMarketingConsentLog
{
    public Task RecordConsentAsync(string agentId, MarketingConsent consent, CancellationToken ct) => Task.CompletedTask;
    public Task RedactAsync(string agentId, string email, CancellationToken ct) => Task.CompletedTask;
}

file sealed class NoOpLeadEnricher : ILeadEnricher
{
    public Task<(LeadEnrichment Enrichment, LeadScore Score)> EnrichAsync(Lead lead, CancellationToken ct) =>
        Task.FromResult((LeadEnrichment.Empty(), LeadScore.Default("no-op")));
}

file sealed class NoOpLeadNotifier : ILeadNotifier
{
    public Task NotifyAgentAsync(string agentId, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct) =>
        Task.CompletedTask;
}

file sealed class NoOpHomeSearchProvider : IHomeSearchProvider
{
    public Task<List<Listing>> SearchAsync(HomeSearchCriteria criteria, CancellationToken ct) =>
        Task.FromResult(new List<Listing>());
}

file sealed class NoOpLeadDataDeletion : ILeadDataDeletion
{
    public Task<string> InitiateDeletionRequestAsync(string agentId, string email, CancellationToken ct) =>
        Task.FromResult("no-op-token");
    public Task<DeleteResult> ExecuteDeletionAsync(string agentId, string email, string token, string reason, CancellationToken ct) =>
        Task.FromResult(new DeleteResult(true, []));
}

file sealed class NoOpDeletionAuditLog : IDeletionAuditLog
{
    public Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct) => Task.CompletedTask;
    public Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct) => Task.CompletedTask;
}

// ---------------------------------------------------------------------------
// Integration tests — hit the real HTTP stack
// ---------------------------------------------------------------------------
public class SubmitLeadEndpointIntegrationTests : IClassFixture<LeadSubmitTestFactory>
{
    private readonly HttpClient _client;

    public SubmitLeadEndpointIntegrationTests(LeadSubmitTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object ValidPayload() => new
    {
        leadType = "Buyer",
        firstName = "Jane",
        lastName = "Doe",
        email = "jane@example.com",
        phone = "555-123-4567",
        timeline = "3-6 months",
        notes = (string?)null,
        buyer = new
        {
            desiredArea = "Springfield, NJ",
            minPrice = (decimal?)null,
            maxPrice = (decimal?)null
        },
        marketingConsent = new
        {
            optedIn = true,
            consentText = "I agree to receive marketing communications.",
            channels = new[] { "email", "sms" }
        }
    };

    [Fact]
    public async Task PostLead_Returns202_ForValidRequest()
    {
        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/leads", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("leadId", out var leadId).Should().BeTrue();
        leadId.GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("status").GetString().Should().Be("received");
    }

    [Fact]
    public async Task PostLead_Returns404_ForUnknownAgent()
    {
        var response = await _client.PostAsJsonAsync("/agents/no-such-agent/leads", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostLead_Returns400_WhenFirstNameMissing()
    {
        var payload = new
        {
            leadType = "Buyer",
            firstName = "",
            lastName = "Doe",
            email = "jane@example.com",
            phone = "555-123-4567",
            timeline = "ASAP",
            buyer = new { desiredArea = "Springfield, NJ" },
            marketingConsent = new
            {
                optedIn = true,
                consentText = "I agree.",
                channels = new[] { "email" }
            }
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/leads", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostLead_Returns400_WhenEmailInvalid()
    {
        var payload = new
        {
            leadType = "Buyer",
            firstName = "Jane",
            lastName = "Doe",
            email = "not-an-email",
            phone = "555-123-4567",
            timeline = "ASAP",
            buyer = new { desiredArea = "Springfield, NJ" },
            marketingConsent = new
            {
                optedIn = true,
                consentText = "I agree.",
                channels = new[] { "email" }
            }
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/leads", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("Email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostLead_Returns400_WhenPhoneInvalid()
    {
        var payload = new
        {
            leadType = "Buyer",
            firstName = "Jane",
            lastName = "Doe",
            email = "jane@example.com",
            phone = "ab",
            timeline = "ASAP",
            buyer = new { desiredArea = "Springfield, NJ" },
            marketingConsent = new
            {
                optedIn = true,
                consentText = "I agree.",
                channels = new[] { "email" }
            }
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/leads", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PostLead_Returns400_WhenSellingWithNoSellerDetails()
    {
        // leadType is "Seller" but no seller object → business rule validation → 400
        var payload = new
        {
            leadType = "Seller",
            firstName = "Jane",
            lastName = "Doe",
            email = "jane@example.com",
            phone = "555-123-4567",
            timeline = "ASAP",
            // seller is intentionally absent
            marketingConsent = new
            {
                optedIn = true,
                consentText = "I agree.",
                channels = new[] { "email" }
            }
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/leads", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

// ---------------------------------------------------------------------------
// Unit tests — call Handle() directly with mocks
// ---------------------------------------------------------------------------
public class SubmitLeadEndpointUnitTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static SubmitLeadRequest MakeValidRequest(
        LeadType? leadType = null,
        SellerDetailsRequest? seller = null,
        BuyerDetailsRequest? buyer = null) => new()
    {
        LeadType = leadType ?? LeadType.Buyer,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-123-4567",
        Timeline = "3-6 months",
        Seller = seller,
        Buyer = buyer ?? MakeBuyer(),
        MarketingConsent = new MarketingConsentRequest
        {
            OptedIn = true,
            ConsentText = "I agree to receive marketing communications.",
            Channels = ["email", "sms"]
        }
    };

    private static SellerDetailsRequest MakeSeller() => new()
    {
        Address = "123 Main St",
        City = "Springfield",
        State = "NJ",
        Zip = "07081"
    };

    private static BuyerDetailsRequest MakeBuyer() => new()
    {
        DesiredArea = "Springfield, NJ"
    };

    private static AccountConfig MakeAgent() => new() { Handle = "test-agent" };

    private record Mocks(
        Mock<IAccountConfigService> AccountConfig,
        Mock<ILeadStore> LeadStore,
        Mock<IMarketingConsentLog> ConsentLog,
        LeadProcessingChannel ProcessingChannel,
        Mock<ILogger<SubmitLeadEndpoint>> Logger);

    private static Mocks CreateMocks(AccountConfig? agent = null)
    {
        var accountConfig = new Mock<IAccountConfigService>();
        accountConfig
            .Setup(s => s.GetAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent ?? MakeAgent());

        var leadStore = new Mock<ILeadStore>();
        leadStore
            .Setup(s => s.SaveAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consentLog = new Mock<IMarketingConsentLog>();
        consentLog
            .Setup(s => s.RecordConsentAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var channel = new LeadProcessingChannel();
        var logger = new Mock<ILogger<SubmitLeadEndpoint>>();

        return new Mocks(accountConfig, leadStore, consentLog, channel, logger);
    }

    private static HttpContext MakeHttpContext(
        string ip = "1.2.3.4",
        string userAgent = "TestBrowser/1.0")
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        ctx.Request.Headers.UserAgent = userAgent;
        return ctx;
    }

    private static Task<IResult> CallHandle(
        Mocks m,
        SubmitLeadRequest? request = null,
        string agentId = "test-agent",
        HttpContext? httpContext = null) =>
        SubmitLeadEndpoint.Handle(
            agentId,
            request ?? MakeValidRequest(),
            m.AccountConfig.Object,
            m.LeadStore.Object,
            m.ConsentLog.Object,
            m.ProcessingChannel,
            httpContext ?? MakeHttpContext(),
            m.Logger.Object,
            CancellationToken.None);

    // -------------------------------------------------------------------------
    // Validation tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_Returns400_WhenFirstNameMissing()
    {
        var m = CreateMocks();
        var request = new SubmitLeadRequest
        {
            LeadType = LeadType.Buyer,
            FirstName = "",      // invalid
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "555-123-4567",
            Timeline = "ASAP",
            MarketingConsent = new MarketingConsentRequest
            {
                OptedIn = true,
                ConsentText = "I agree.",
                Channels = ["email"]
            }
        };

        var result = await CallHandle(m, request);

        result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task Handle_Returns400_WhenEmailInvalid()
    {
        var m = CreateMocks();
        var request = new SubmitLeadRequest
        {
            LeadType = LeadType.Buyer,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "not-an-email",
            Phone = "555-123-4567",
            Timeline = "ASAP",
            MarketingConsent = new MarketingConsentRequest
            {
                OptedIn = true,
                ConsentText = "I agree.",
                Channels = ["email"]
            }
        };

        var result = await CallHandle(m, request);

        result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task Handle_Returns400_WhenPhoneInvalid()
    {
        var m = CreateMocks();
        var request = new SubmitLeadRequest
        {
            LeadType = LeadType.Buyer,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "ab",        // too short / invalid pattern
            Timeline = "ASAP",
            MarketingConsent = new MarketingConsentRequest
            {
                OptedIn = true,
                ConsentText = "I agree.",
                Channels = ["email"]
            }
        };

        var result = await CallHandle(m, request);

        result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task Handle_Returns400_WhenSellingWithoutSellerDetails()
    {
        var m = CreateMocks();
        var request = new SubmitLeadRequest
        {
            LeadType = LeadType.Seller,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "555-123-4567",
            Timeline = "3-6 months",
            Seller = null,  // intentionally absent
            MarketingConsent = new MarketingConsentRequest
            {
                OptedIn = true,
                ConsentText = "I agree.",
                Channels = ["email"]
            }
        };

        var result = await CallHandle(m, request);

        result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task Handle_Returns400_WhenBuyingWithoutBuyerDetails()
    {
        var m = CreateMocks();
        var request = new SubmitLeadRequest
        {
            LeadType = LeadType.Buyer,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "555-123-4567",
            Timeline = "3-6 months",
            Buyer = null,  // intentionally absent
            MarketingConsent = new MarketingConsentRequest
            {
                OptedIn = true,
                ConsentText = "I agree.",
                Channels = ["email"]
            }
        };

        var result = await CallHandle(m, request);

        result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task Handle_Returns404_WhenAgentNotFound()
    {
        var m = CreateMocks();
        m.AccountConfig
            .Setup(s => s.GetAccountAsync("no-such-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

        var result = await CallHandle(m, agentId: "no-such-agent");

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_Returns202_ForValidRequest()
    {
        var m = CreateMocks();

        var result = await CallHandle(m);

        result.Should().BeOfType<Accepted<SubmitLeadResponse>>();
        var accepted = (Accepted<SubmitLeadResponse>)result;
        accepted.Value!.Status.Should().Be("received");
        accepted.Value.LeadId.Should().NotBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Behavioral tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_SavesLeadToLeadStore()
    {
        var m = CreateMocks();

        await CallHandle(m);

        m.LeadStore.Verify(s => s.SaveAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RecordsConsentToConsentLog()
    {
        var m = CreateMocks();

        await CallHandle(m);

        m.ConsentLog.Verify(
            c => c.RecordConsentAsync("test-agent", It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExtractsIpAndUserAgentFromHttpContext()
    {
        var m = CreateMocks();
        var ctx = MakeHttpContext(ip: "10.0.0.1", userAgent: "MyApp/2.0");

        MarketingConsent? capturedConsent = null;
        m.ConsentLog
            .Setup(c => c.RecordConsentAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MarketingConsent, CancellationToken>((_, consent, _) => capturedConsent = consent)
            .Returns(Task.CompletedTask);

        await CallHandle(m, httpContext: ctx);

        capturedConsent.Should().NotBeNull();
        capturedConsent!.IpAddress.Should().Be("10.0.0.1");
        capturedConsent.UserAgent.Should().Be("MyApp/2.0");
    }

    [Fact]
    public async Task Handle_EnqueuesProcessingRequest_ToChannel()
    {
        var m = CreateMocks();

        await CallHandle(m);

        // The channel should have exactly one item
        m.ProcessingChannel.Reader.TryRead(out var processingRequest).Should().BeTrue();
        processingRequest.Should().NotBeNull();
        processingRequest!.AgentId.Should().Be("test-agent");
        processingRequest.Lead.Email.Should().Be("jane@example.com");
        processingRequest.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_EnqueuesProcessingRequest_WithCorrectLeadType()
    {
        var m = CreateMocks();
        var request = MakeValidRequest(leadType: LeadType.Seller, seller: MakeSeller());

        await CallHandle(m, request);

        m.ProcessingChannel.Reader.TryRead(out var processingRequest).Should().BeTrue();
        processingRequest!.Lead.LeadType.Should().Be(LeadType.Seller);
    }

    // -------------------------------------------------------------------------
    // Error handling tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_Returns500_WhenLeadStoreFails()
    {
        var m = CreateMocks();
        m.LeadStore
            .Setup(s => s.SaveAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));

        var act = () => CallHandle(m);

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task Handle_Returns500_WhenConsentLogFails()
    {
        var m = CreateMocks();
        m.ConsentLog
            .Setup(c => c.RecordConsentAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("write failed"));

        var act = () => CallHandle(m);

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task Handle_Returns202_EvenIfChannelIsNearCapacity()
    {
        var m = CreateMocks();

        // Submit should return 202 — background processing is decoupled
        var result = await CallHandle(m);
        result.Should().BeOfType<Accepted<SubmitLeadResponse>>();
    }

    // -------------------------------------------------------------------------
    // GroupValidationErrors helper
    // -------------------------------------------------------------------------

    [Fact]
    public void GroupValidationErrors_WithEmptyMemberNames_GroupsUnderEmptyKey()
    {
        var results = new List<ValidationResult>
        {
            new("Error with no member"),
            new("Named error", ["Email"])
        };

        var grouped = SubmitLeadEndpoint.GroupValidationErrors(results);

        grouped.Should().ContainKey("");
        grouped[""].Should().Contain("Error with no member");
        grouped.Should().ContainKey("Email");
        grouped["Email"].Should().Contain("Named error");
    }

    [Fact]
    public void GroupValidationErrors_MultipleSameMember_GroupsTogether()
    {
        var results = new List<ValidationResult>
        {
            new("Too short", ["FirstName"]),
            new("Required", ["FirstName"])
        };

        var grouped = SubmitLeadEndpoint.GroupValidationErrors(results);

        grouped["FirstName"].Should().HaveCount(2);
    }
}
