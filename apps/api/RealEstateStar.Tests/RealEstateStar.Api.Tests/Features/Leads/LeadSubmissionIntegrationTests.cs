using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.Domain.Privacy;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Api.Tests.Integration;

namespace RealEstateStar.Api.Tests.Features.Leads;

// ---------------------------------------------------------------------------
// Factory — injects Moq mocks as singletons so tests can verify interactions.
// Mocks are created per-test via a factory that the collection fixture exposes.
// ---------------------------------------------------------------------------

/// <summary>
/// Holds the verifiable mocks that are injected into the test server.
/// One instance is created per test via <see cref="LeadSubmissionMockFactory"/>.
/// </summary>
public class LeadSubmissionMocks
{
    public Mock<ILeadStore> LeadStore { get; } = new();
    public Mock<IMarketingConsentLog> ConsentLog { get; } = new();
    // TODO: Pipeline redesign — ILeadEnricher and ILeadNotifier removed in Phase 1.5; replaced in Phase 2/3/4
    // public Mock<ILeadEnricher> Enricher { get; } = new();
    // public Mock<ILeadNotifier> Notifier { get; }
    public Mock<IHomeSearchProvider> HomeSearch { get; } = new();
    public Mock<ILeadDataDeletion> Deletion { get; } = new();
    public Mock<IDeletionAuditLog> AuditLog { get; } = new();

    public LeadSubmissionMocks()
    {
        // TODO: Pipeline redesign — Notifier mock setup removed in Phase 1.5
    }
}

/// <summary>
/// WebApplicationFactory variant that accepts a pre-built <see cref="LeadSubmissionMocks"/>
/// and registers those mocks into the DI container, replacing the no-op stubs from
/// the base <see cref="TestWebApplicationFactory"/>.
/// </summary>
public class LeadSubmissionMockFactory(LeadSubmissionMocks mocks) : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder); // registers no-ops first

        // Override with verifiable mocks (last registration wins)
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(mocks.LeadStore.Object);
            services.AddSingleton(mocks.ConsentLog.Object);
            // TODO: Pipeline redesign — ILeadEnricher and ILeadNotifier removed in Phase 1.5; replaced in Phase 2/3/4
            // services.AddSingleton(mocks.Enricher.Object);
            // services.AddSingleton(mocks.Notifier.Object);
            services.AddSingleton(mocks.HomeSearch.Object);
            services.AddSingleton(mocks.Deletion.Object);
            services.AddSingleton(mocks.AuditLog.Object);
        });
    }
}

// ---------------------------------------------------------------------------
// Shared helper — builds HTTP requests for the lead pipeline endpoints.
// ---------------------------------------------------------------------------
internal static class LeadRequests
{
    internal static object SellerPayload(
        string email = "jane@example.com",
        bool marketingOptIn = true) => new
        {
            leadType = "seller",
            firstName = "Jane",
            lastName = "Doe",
            email,
            phone = "555-123-4567",
            timeline = "3-6 months",
            seller = new
            {
                address = "123 Main St",
                city = "Springfield",
                state = "NJ",
                zip = "07081"
            },
            marketingConsent = new
            {
                optedIn = marketingOptIn,
                consentText = "I agree to receive marketing communications.",
                channels = new[] { "email", "sms" }
            }
        };

    internal static object BuyerPayload(string email = "buyer@example.com") => new
    {
        leadType = "buyer",
        firstName = "Bob",
        lastName = "Smith",
        email,
        phone = "555-987-6543",
        timeline = "1-3 months",
        buyer = new
        {
            desiredArea = "Springfield, NJ",
            minPrice = (decimal?)200_000,
            maxPrice = (decimal?)400_000,
            minBeds = (int?)3,
            minBaths = (int?)2
        },
        marketingConsent = new
        {
            optedIn = true,
            consentText = "I agree.",
            channels = new[] { "email" }
        }
    };

    internal static Lead MakeLead(
        string agentId = "jenise-buckalew",
        string email = "jane@example.com",
        string consentToken = "tok-abc123",
        bool marketingOptedIn = true) => new()
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LeadType = LeadType.Seller,
            FirstName = "Jane",
            LastName = "Doe",
            Email = email,
            Phone = "555-123-4567",
            Timeline = "3-6 months",
            ConsentToken = consentToken,
            MarketingOptedIn = marketingOptedIn,
            Status = LeadStatus.Received,
            SellerDetails = new SellerDetails
            {
                Address = "123 Main St",
                City = "Springfield",
                State = "NJ",
                Zip = "07081"
            }
        };
}

// ---------------------------------------------------------------------------
// Test 1 — Full submission flow
// POST /agents/{agentId}/leads with a seller lead
//   → 202 returned immediately
//   → lead saved to ILeadStore
//   → consent logged to IMarketingConsentLog
// TODO: Pipeline redesign — ILeadEnricher/ILeadNotifier verification removed in Phase 1.5; will be re-added in Phase 2/3/4
// ---------------------------------------------------------------------------
public class LeadSubmission_FullSubmissionFlowTests
{
    private const string AgentId = "jenise-buckalew";

    [Fact]
    public async Task SubmitSellerLead_Returns202_AndPersistsLeadAndConsent()
    {
        var mocks = new LeadSubmissionMocks();

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads",
            LeadRequests.SellerPayload());

        // 202 returned immediately
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("leadId", out var leadIdProp).Should().BeTrue();
        leadIdProp.GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("status").GetString().Should().Be("received");

        // Lead saved to store before returning 202
        mocks.LeadStore.Verify(
            s => s.SaveAsync(
                It.Is<Lead>(l =>
                    l.AgentId == AgentId &&
                    l.Email == "jane@example.com" &&
                    (l.LeadType == LeadType.Seller || l.LeadType == LeadType.Both)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Consent logged before returning 202
        mocks.ConsentLog.Verify(
            c => c.RecordConsentAsync(
                AgentId,
                It.Is<MarketingConsent>(mc =>
                    mc.Email == "jane@example.com" &&
                    mc.OptedIn == true &&
                    mc.Channels.Contains("email")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // TODO: Pipeline redesign — SubmitSellerLead_CallsEnricherAndNotifier_InBackground removed in Phase 1.5
    // Will be re-added in Phase 2/3/4 with new pipeline interfaces

    [Fact(Skip = "Requires full orchestrator mock setup — covered by LeadPipelineIntegrationTests in Workers.Lead.Orchestrator.Tests")]
    public async Task SubmitBuyerLead_TriggersHomeSearch_NotCma()
    {
        var mocks = new LeadSubmissionMocks();
        // Pipeline complete signal — orchestrator calls UpdateStatusAsync(lead, LeadStatus.Complete)
        var pipelineDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        mocks.HomeSearch
            .Setup(h => h.SearchAsync(It.IsAny<HomeSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Listing("1 Oak Ave", "Springfield", "NJ", "07081", 350_000m, 3, 2m, 1500, null, null)
            ]);

        mocks.LeadStore
            .Setup(s => s.UpdateStatusAsync(It.IsAny<Lead>(), LeadStatus.Complete, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<Lead, LeadStatus, CancellationToken>((_, _, _) => pipelineDone.TrySetResult());

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        await client.PostAsJsonAsync($"/agents/{AgentId}/leads", LeadRequests.BuyerPayload());

        // Wait for the orchestrator to complete the full pipeline (with timeout)
        await pipelineDone.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Home search was triggered by the orchestrator
        mocks.HomeSearch.Verify(
            h => h.SearchAsync(
                It.Is<HomeSearchCriteria>(c => c.MinBeds == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitLead_Returns404_ForUnknownAgent()
    {
        var mocks = new LeadSubmissionMocks();
        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/agents/no-such-agent/leads",
            LeadRequests.SellerPayload());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        mocks.LeadStore.Verify(s => s.SaveAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitLead_Returns400_WhenSellerLeadTypeWithoutSellerDetails()
    {
        var mocks = new LeadSubmissionMocks();
        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var payload = new
        {
            leadType = "seller",
            firstName = "Jane",
            lastName = "Doe",
            email = "jane@example.com",
            phone = "555-123-4567",
            timeline = "ASAP",
            // seller intentionally omitted
            marketingConsent = new
            {
                optedIn = true,
                consentText = "I agree.",
                channels = new[] { "email" }
            }
        };

        var response = await client.PostAsJsonAsync($"/agents/{AgentId}/leads", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        mocks.LeadStore.Verify(s => s.SaveAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

// ---------------------------------------------------------------------------
// Test 2 — Deletion flow
// POST request-deletion → DELETE data (with token) → lead removed → consent redacted
// ---------------------------------------------------------------------------
public class LeadSubmission_DeletionFlowTests
{
    private const string AgentId = "jenise-buckalew";

    [Fact]
    public async Task DeletionFlow_RequestThenExecute_ReturnsOkAndConfirmsRemoval()
    {
        const string email = "jane@example.com";
        const string token = "del-tok-abc123";

        var lead = LeadRequests.MakeLead(email: email);
        var mocks = new LeadSubmissionMocks();

        // Request deletion — lead found, token issued, audit logged
        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        mocks.Deletion
            .Setup(d => d.InitiateDeletionRequestAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Execute deletion — returns success
        mocks.Deletion
            .Setup(d => d.ExecuteDeletionAsync(AgentId, email, token, "user_request", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(true, ["lead.md", "consent.jsonl"]));

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        // Step 1: request deletion
        var requestResponse = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads/request-deletion",
            new { email });

        requestResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        mocks.Deletion.Verify(
            d => d.InitiateDeletionRequestAsync(AgentId, email, It.IsAny<CancellationToken>()),
            Times.Once);

        mocks.AuditLog.Verify(
            a => a.RecordInitiationAsync(AgentId, lead.Id, email, It.IsAny<CancellationToken>()),
            Times.Once);

        // Step 2: execute deletion with verified token
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/agents/{AgentId}/leads/data")
        {
            Content = JsonContent.Create(new { email, token, reason = "user_request" })
        };
        var deleteResponse = await client.SendAsync(deleteRequest);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("deletedItems", out var deletedItems).Should().BeTrue();
        deletedItems.GetArrayLength().Should().Be(2);

        mocks.Deletion.Verify(
            d => d.ExecuteDeletionAsync(AgentId, email, token, "user_request", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestDeletion_Returns202_EvenWhenEmailNotFound()
    {
        // Anti-enumeration: always 202 whether email exists or not
        var mocks = new LeadSubmissionMocks();

        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, "nobody@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads/request-deletion",
            new { email = "nobody@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Deletion should NOT be initiated for unknown email
        mocks.Deletion.Verify(
            d => d.InitiateDeletionRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteData_Returns404_WhenLeadNotFound()
    {
        var mocks = new LeadSubmissionMocks();

        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, "ghost@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/agents/{AgentId}/leads/data")
        {
            Content = JsonContent.Create(new { email = "ghost@example.com", token = "any-token", reason = "user_request" })
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteData_Returns401_WhenTokenInvalidOrExpired()
    {
        const string email = "jane@example.com";
        var lead = LeadRequests.MakeLead(email: email);
        var mocks = new LeadSubmissionMocks();

        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        mocks.Deletion
            .Setup(d => d.ExecuteDeletionAsync(AgentId, email, "bad-token", "gdpr_erasure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(false, [], "Token invalid"));

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/agents/{AgentId}/leads/data")
        {
            Content = JsonContent.Create(new { email, token = "bad-token", reason = "gdpr_erasure" })
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteData_Returns409_WhenAlreadyDeleted()
    {
        const string email = "jane@example.com";
        var lead = LeadRequests.MakeLead(email: email);
        var mocks = new LeadSubmissionMocks();

        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        mocks.Deletion
            .Setup(d => d.ExecuteDeletionAsync(AgentId, email, "stale-token", "ccpa_deletion", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult(false, [], "already deleted"));

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/agents/{AgentId}/leads/data")
        {
            Content = JsonContent.Create(new { email, token = "stale-token", reason = "ccpa_deletion" })
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

// ---------------------------------------------------------------------------
// Test 3 — Opt-out / re-subscribe flow
// Submit lead with opt-in → POST opt-out → verify marketing_opted_in = false
// → POST subscribe → verify marketing_opted_in = true
// ---------------------------------------------------------------------------
public class LeadSubmission_OptOutSubscribeFlowTests
{
    private const string AgentId = "jenise-buckalew";

    [Fact]
    public async Task OptOut_WithValidToken_UpdatesMarketingOptInToFalse_AndLogsConsent()
    {
        const string email = "jane@example.com";
        const string token = "tok-abc123";

        var lead = LeadRequests.MakeLead(email: email, consentToken: token, marketingOptedIn: true);
        var mocks = new LeadSubmissionMocks();

        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        mocks.LeadStore
            .Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads/opt-out",
            new { email, token });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("opted_out");

        // marketing_opted_in updated to false
        mocks.LeadStore.Verify(
            s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()),
            Times.Once);

        // Consent logged with opt-out action
        mocks.ConsentLog.Verify(
            c => c.RecordConsentAsync(
                AgentId,
                It.Is<MarketingConsent>(mc =>
                    mc.Email == email &&
                    mc.OptedIn == false &&
                    mc.Action == ConsentAction.OptOut &&
                    mc.Source == ConsentSource.EmailLink),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OptOut_WithInvalidToken_Returns200_ButDoesNotWriteAnything()
    {
        const string email = "jane@example.com";

        var lead = LeadRequests.MakeLead(email: email, consentToken: "real-token");
        var mocks = new LeadSubmissionMocks();

        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads/opt-out",
            new { email, token = "wrong-token" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // No writes — invalid token
        mocks.LeadStore.Verify(
            s => s.UpdateMarketingOptInAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);

        mocks.ConsentLog.Verify(
            c => c.RecordConsentAsync(It.IsAny<string>(), It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OptOut_ThenSubscribe_FlipsMarketingOptInBothWays()
    {
        const string email = "jane@example.com";
        const string token = "tok-abc123";

        // Lead starts opted in
        var lead = LeadRequests.MakeLead(email: email, consentToken: token, marketingOptedIn: true);
        var mocks = new LeadSubmissionMocks();

        // Both opt-out and subscribe look up the same lead
        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        mocks.LeadStore
            .Setup(s => s.UpdateMarketingOptInAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        // Step 1: opt out
        var optOutResponse = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads/opt-out",
            new { email, token });
        optOutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify opt-out wrote false
        mocks.LeadStore.Verify(
            s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()),
            Times.Once);

        // Step 2: re-subscribe
        var subscribeResponse = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads/subscribe",
            new { email, token });
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var subscribeBody = await subscribeResponse.Content.ReadFromJsonAsync<JsonElement>();
        subscribeBody.GetProperty("status").GetString().Should().Be("subscribed");

        // Verify subscribe wrote true
        mocks.LeadStore.Verify(
            s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, true, It.IsAny<CancellationToken>()),
            Times.Once);

        // Consent log called twice — once for opt-out, once for subscribe
        mocks.ConsentLog.Verify(
            c => c.RecordConsentAsync(AgentId, It.IsAny<MarketingConsent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task OptOut_IsIdempotent_WhenLeadAlreadyOptedOut()
    {
        const string email = "jane@example.com";
        const string token = "tok-abc123";

        // Lead already opted out
        var lead = LeadRequests.MakeLead(email: email, consentToken: token, marketingOptedIn: false);
        var mocks = new LeadSubmissionMocks();

        mocks.LeadStore
            .Setup(s => s.GetByEmailAsync(AgentId, email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        mocks.LeadStore
            .Setup(s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var factory = new LeadSubmissionMockFactory(mocks);
        var client = factory.CreateClient();

        // Opt out again — should still succeed idempotently
        var response = await client.PostAsJsonAsync(
            $"/agents/{AgentId}/leads/opt-out",
            new { email, token });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // UpdateMarketingOptInAsync still called — the endpoint is idempotent (always writes)
        mocks.LeadStore.Verify(
            s => s.UpdateMarketingOptInAsync(AgentId, lead.Id, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
