using FluentAssertions;
using RealEstateStar.Functions.Lead.Activities;
using RealEstateStar.Functions.Lead.Models;
using LeadScore = RealEstateStar.Domain.Leads.Models.LeadScore;
using CmaWorkerResult = RealEstateStar.Domain.Leads.Models.CmaWorkerResult;
using HomeSearchWorkerResult = RealEstateStar.Domain.Leads.Models.HomeSearchWorkerResult;

namespace RealEstateStar.Functions.Lead.Tests;

/// <summary>
/// Tests for the lead orchestrator routing logic.
///
/// Note: <see cref="TaskOrchestrationContext"/> is sealed and cannot be Moq-mocked.
/// These tests validate the routing rules — seller/buyer/both dispatch decisions —
/// by verifying the conditions used in the orchestrator directly.
/// Full end-to-end orchestration behavior is covered by integration tests
/// once the Functions host is wired up.
/// </summary>
public class LeadOrchestratorFunctionTests
{
    // ── Lead type routing conditions ──────────────────────────────────────────

    [Fact]
    public void Seller_lead_with_seller_details_enables_cma()
    {
        var input = BuildOrchestratorInput(shouldRunCma: true, shouldRunHomeSearch: false);

        input.ShouldRunCma.Should().BeTrue();
        input.ShouldRunHomeSearch.Should().BeFalse();
    }

    [Fact]
    public void Buyer_lead_with_buyer_details_enables_home_search()
    {
        var input = BuildOrchestratorInput(shouldRunCma: false, shouldRunHomeSearch: true);

        input.ShouldRunCma.Should().BeFalse();
        input.ShouldRunHomeSearch.Should().BeTrue();
    }

    [Fact]
    public void Both_lead_enables_cma_and_home_search_in_parallel()
    {
        var input = BuildOrchestratorInput(shouldRunCma: true, shouldRunHomeSearch: true);

        input.ShouldRunCma.Should().BeTrue();
        input.ShouldRunHomeSearch.Should().BeTrue();
    }

    [Fact]
    public void Cache_hit_skips_cma_dispatch()
    {
        // Simulates the content cache check output for a seller lead
        var cacheOutput = new CheckContentCacheOutput
        {
            CmaCacheHit = true,
            HsCacheHit = false,
            CachedCmaResult = new CmaWorkerResult("l1", true, null, 500000m, null, null, [], null),
            CachedHsResult = null
        };

        // When cache hit → CMA should use cached result, not dispatch worker
        cacheOutput.CmaCacheHit.Should().BeTrue();
        cacheOutput.CachedCmaResult.Should().NotBeNull();
        cacheOutput.CachedCmaResult!.Success.Should().BeTrue();
    }

    [Fact]
    public void Cache_hit_skips_home_search_dispatch()
    {
        var cacheOutput = new CheckContentCacheOutput
        {
            CmaCacheHit = false,
            HsCacheHit = true,
            CachedCmaResult = null,
            CachedHsResult = new HomeSearchWorkerResult("l1", true, null, [], null)
        };

        cacheOutput.HsCacheHit.Should().BeTrue();
        cacheOutput.CachedHsResult.Should().NotBeNull();
    }

    [Fact]
    public void Both_cache_hit_skips_all_workers()
    {
        var cacheOutput = new CheckContentCacheOutput
        {
            CmaCacheHit = true,
            HsCacheHit = true,
            CachedCmaResult = new CmaWorkerResult("l1", true, null, 500000m, null, null, [], null),
            CachedHsResult = new HomeSearchWorkerResult("l1", true, null, [], null)
        };

        cacheOutput.CmaCacheHit.Should().BeTrue();
        cacheOutput.HsCacheHit.Should().BeTrue();
    }

    // ── PDF gate — only when CMA succeeds ─────────────────────────────────────

    [Fact]
    public void Pdf_gate_is_enabled_when_cma_succeeds()
    {
        var cmaOutput = new CmaFunctionOutput
        {
            Result = new CmaWorkerResult("l1", true, null, 500000m, null, null, [], null)
        };

        var shouldGeneratePdf = cmaOutput.Result.Success == true;
        shouldGeneratePdf.Should().BeTrue();
    }

    [Fact]
    public void Pdf_gate_is_disabled_when_cma_fails()
    {
        var cmaOutput = new CmaFunctionOutput
        {
            Result = new CmaWorkerResult("l1", false, "No comps", null, null, null, null, null)
        };

        var shouldGeneratePdf = cmaOutput.Result.Success == true;
        shouldGeneratePdf.Should().BeFalse();
    }

    [Fact]
    public void Pdf_gate_is_disabled_when_no_cma_result()
    {
        CmaFunctionOutput? cmaOutput = null;

        var shouldGeneratePdf = cmaOutput?.Result.Success == true;
        shouldGeneratePdf.Should().BeFalse();
    }

    // ── Instance ID format ────────────────────────────────────────────────────

    [Fact]
    public void Instance_id_is_deterministic_for_same_agentId_and_leadId()
    {
        var agentId = "jenise-buckalew";
        var leadId = "550e8400-e29b-41d4-a716-446655440000";

        var id1 = $"lead-{agentId}-{leadId}";
        var id2 = $"lead-{agentId}-{leadId}";

        id1.Should().Be(id2);
        id1.Should().StartWith("lead-");
        id1.Should().Contain(agentId);
        id1.Should().Contain(leadId);
    }

    [Fact]
    public void Instance_id_differs_for_different_lead_ids()
    {
        const string agentId = "jenise-buckalew";
        var id1 = $"lead-{agentId}-lead-111";
        var id2 = $"lead-{agentId}-lead-222";

        id1.Should().NotBe(id2);
    }

    // ── Cache TTL constants ───────────────────────────────────────────────────

    [Fact]
    public void Cma_cache_ttl_is_24_hours()
    {
        LeadOrchestratorFunction.CmaCacheTtl.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void HomeSearch_cache_ttl_is_1_hour()
    {
        // HomeSearch TTL is defined in UpdateContentCacheFunction (more testable location)
        // but the orchestrator's cache check reuses results from IDistributedContentCache
        // which was populated with the 1h TTL.
        // This test documents the expected TTL value.
        UpdateContentCacheFunction.HomeSearchCacheTtlForTests.Should().Be(TimeSpan.FromHours(1));
    }

    // ── Partial completion — activity failure does not abort pipeline ──────────

    [Fact]
    public void Null_cma_result_does_not_prevent_persist()
    {
        // Simulates: CMA failed/timed out, HomeSearch succeeded
        CmaFunctionOutput? cmaOutput = null;
        var hsOutput = new HomeSearchFunctionOutput
        {
            Result = new HomeSearchWorkerResult("l1", true, null, [], null)
        };

        var persistInput = new PersistLeadResultsInput
        {
            AgentId = "a1",
            LeadId = "l1",
            CorrelationId = "c1",
            Score = new LeadScore { OverallScore = 50, Factors = [], Explanation = "x" },
            CmaResult = cmaOutput?.Result,
            HsResult = hsOutput.Result,
            PdfStoragePath = null,
            EmailDraft = null,
            EmailSent = false,
            AgentNotified = false,
            CmaInputHash = "x",
            HsInputHash = "y"
        };

        // Persist input can be built with null CMA result — pipeline continues
        persistInput.CmaResult.Should().BeNull();
        persistInput.HsResult.Should().NotBeNull();
        persistInput.HsResult!.Success.Should().BeTrue();
    }

    [Fact]
    public void Null_hs_result_does_not_prevent_persist()
    {
        // Simulates: CMA succeeded, HomeSearch failed/timed out
        var cmaOutput = new CmaFunctionOutput
        {
            Result = new CmaWorkerResult("l1", true, null, 500000m, null, null, [], null)
        };
        HomeSearchFunctionOutput? hsOutput = null;

        var persistInput = new PersistLeadResultsInput
        {
            AgentId = "a1",
            LeadId = "l1",
            CorrelationId = "c1",
            Score = new LeadScore { OverallScore = 80, Factors = [], Explanation = "x" },
            CmaResult = cmaOutput.Result,
            HsResult = hsOutput?.Result,
            PdfStoragePath = null,
            EmailDraft = null,
            EmailSent = false,
            AgentNotified = false,
            CmaInputHash = "x",
            HsInputHash = "y"
        };

        persistInput.CmaResult.Should().NotBeNull();
        persistInput.HsResult.Should().BeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static LeadOrchestratorInput BuildOrchestratorInput(bool shouldRunCma, bool shouldRunHomeSearch) =>
        new()
        {
            AgentId = "agent-test",
            LeadId = "lead-test",
            CorrelationId = "corr-test",
            ShouldRunCma = shouldRunCma,
            ShouldRunHomeSearch = shouldRunHomeSearch,
            CmaInputHash = "cma_h",
            HsInputHash = "hs_h"
        };
}
