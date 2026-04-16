using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.DataServices.Routing;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Tests.Routing;

public class RoutingServiceTests
{
    private readonly Mock<IRoutingPolicyStore> _policyStore = new(MockBehavior.Strict);
    private readonly Mock<IBrokerageRoutingConsumptionStore> _consumptionStore = new(MockBehavior.Strict);
    private readonly RoutingService _sut;

    private const string AccountId = "brokerage-1";
    private const string LeadId = "lead-abc";
    private static readonly CancellationToken Ct = CancellationToken.None;

    public RoutingServiceTests()
    {
        _sut = new RoutingService(
            _policyStore.Object,
            _consumptionStore.Object,
            NullLogger<RoutingService>.Instance);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static RoutingPolicy BuildPolicy(
        string? nextLead = null,
        params (string id, bool enabled, string[] areas)[] agents)
    {
        var agentList = agents.Select(a => new RoutingAgent
        {
            AgentId = a.id,
            Enabled = a.enabled,
            ServiceAreas = a.areas,
            Weight = 1
        }).ToArray();

        return new RoutingPolicy
        {
            AccountId = AccountId,
            Agents = agentList,
            NextLead = nextLead,
            Strategy = "round-robin"
        };
    }

    private static BrokerageRoutingConsumption BuildConsumption(
        string policyHash,
        int counter = 0,
        bool overrideConsumed = false,
        string? etag = "\"etag-1\"")
        => new()
        {
            AccountId = AccountId,
            PolicyContentHash = policyHash,
            Counter = counter,
            OverrideConsumed = overrideConsumed,
            LastDecisionAt = DateTime.UtcNow,
            ETag = etag
        };

    // ─── Null policy ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_NullPolicy_Throws()
    {
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct))
            .ReturnsAsync((RoutingPolicy?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RouteLeadAsync(AccountId, LeadId, null, Ct));
    }

    // ─── Override consumption ─────────────────────────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_OverrideSet_NotConsumed_RoutesToNamedAgent()
    {
        var policy = BuildPolicy(
            nextLead: "agent-vip",
            ("agent-vip", true, []),
            ("agent-a", true, []),
            ("agent-b", true, []));

        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // Consumption exists with matching hash and override not consumed
        var consumption = BuildConsumption(ComputePolicyHash(policy), counter: 0, overrideConsumed: false);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(consumption);

        // CAS succeeds first try
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.OverrideConsumed == true), Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);

        Assert.Equal("agent-vip", decision.AgentId);
        Assert.Equal("override", decision.Reason);
        Assert.Equal(1, decision.AttemptCount);
    }

    [Fact]
    public async Task RouteLeadAsync_OverrideAlreadyConsumed_FallsToRoundRobin()
    {
        var policy = BuildPolicy(
            nextLead: "agent-vip",
            ("agent-vip", true, []),
            ("agent-a", true, []),
            ("agent-b", true, []));

        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        var consumption = BuildConsumption(ComputePolicyHash(policy), counter: 1, overrideConsumed: true);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(consumption);

        // CAS to increment counter succeeds
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 2 && c.OverrideConsumed == true), Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);

        Assert.Equal("round-robin", decision.Reason);
    }

    // ─── Round-robin cycling ──────────────────────────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_RoundRobin_CyclesAcrossThreeAgents()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-a", true, []),
            ("agent-b", true, []),
            ("agent-c", true, []));

        var hash = ComputePolicyHash(policy);
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // Request 1 → counter=0 → agent-a
        var c0 = BuildConsumption(hash, counter: 0);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(c0);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 1), Ct))
            .ReturnsAsync(true);

        var d1 = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);
        Assert.Equal("agent-a", d1.AgentId);

        // Request 2 → counter=1 → agent-b
        var c1 = BuildConsumption(hash, counter: 1);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(c1);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 2), Ct))
            .ReturnsAsync(true);

        var d2 = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);
        Assert.Equal("agent-b", d2.AgentId);

        // Request 3 → counter=2 → agent-c
        var c2 = BuildConsumption(hash, counter: 2);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(c2);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 3), Ct))
            .ReturnsAsync(true);

        var d3 = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);
        Assert.Equal("agent-c", d3.AgentId);

        // Request 4 → counter=3 → wraps to agent-a
        var c3 = BuildConsumption(hash, counter: 3);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(c3);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 4), Ct))
            .ReturnsAsync(true);

        var d4 = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);
        Assert.Equal("agent-a", d4.AgentId);
    }

    // ─── Single-agent policy ──────────────────────────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_SingleAgent_AlwaysRoutesToThatAgent()
    {
        var policy = BuildPolicy(nextLead: null, ("agent-solo", true, []));
        var hash = ComputePolicyHash(policy);

        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        for (int counter = 0; counter < 5; counter++)
        {
            var consumption = BuildConsumption(hash, counter: counter);
            _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(consumption);
            _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                    It.Is<BrokerageRoutingConsumption>(c => c.Counter == counter + 1), Ct))
                .ReturnsAsync(true);

            var decision = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);
            Assert.Equal("agent-solo", decision.AgentId);
        }
    }

    // ─── Disabled agent skipped ───────────────────────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_DisabledAgentSkipped_OnlyEnabledReceiveLeads()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-a", true, []),
            ("agent-disabled", false, []),
            ("agent-b", true, []));

        var hash = ComputePolicyHash(policy);
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // counter=0 → index 0 of enabled=[a,b] → agent-a
        var c0 = BuildConsumption(hash, counter: 0);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(c0);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 1), Ct))
            .ReturnsAsync(true);

        var d1 = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);
        Assert.Equal("agent-a", d1.AgentId);

        // counter=1 → index 1 of enabled=[a,b] → agent-b (not agent-disabled)
        var c1 = BuildConsumption(hash, counter: 1);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(c1);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 2), Ct))
            .ReturnsAsync(true);

        var d2 = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);
        Assert.Equal("agent-b", d2.AgentId);
    }

    [Fact]
    public async Task RouteLeadAsync_AllAgentsDisabled_Throws()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-a", false, []),
            ("agent-b", false, []));

        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct))
            .ReturnsAsync(BuildConsumption(ComputePolicyHash(policy)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RouteLeadAsync(AccountId, LeadId, null, Ct));
    }

    // ─── Hash change resets counter ───────────────────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_HashChangedConsumption_ResetsCounterAndRoutes()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-a", true, []),
            ("agent-b", true, []));

        var currentHash = ComputePolicyHash(policy);
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // Consumption has a stale hash (simulating policy file was edited)
        var staleConsumption = BuildConsumption("stale-hash-from-old-policy", counter: 99);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(staleConsumption);

        // After reset, counter is 0 → agent-a, saves with counter=1
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c =>
                    c.Counter == 1 &&
                    c.PolicyContentHash == currentHash),
                Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);

        Assert.Equal("agent-a", decision.AgentId);
        Assert.Equal("round-robin", decision.Reason);
    }

    [Fact]
    public async Task RouteLeadAsync_NullConsumption_TreatedAsFreshAndRoutes()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-a", true, []),
            ("agent-b", true, []));

        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync((BrokerageRoutingConsumption?)null);

        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 1), Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);

        Assert.Equal("agent-a", decision.AgentId);
        Assert.Equal("round-robin", decision.Reason);
    }

    // ─── Service area preference ──────────────────────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_ServiceAreaMatch_PrefersMatchingAgent()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-south", true, ["miami", "coral gables"]),
            ("agent-north", true, ["fort lauderdale", "boca raton"]));

        var hash = ComputePolicyHash(policy);
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // counter=0, lead service area = "Miami" (case-insensitive match to agent-south)
        var consumption = BuildConsumption(hash, counter: 0);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(consumption);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 1), Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, "Miami", Ct);

        // Only agent-south matches "Miami" → counter 0 % 1 = 0 → agent-south
        Assert.Equal("agent-south", decision.AgentId);
    }

    [Fact]
    public async Task RouteLeadAsync_ServiceAreaNoMatch_RoutesAcrossAllEnabledAgents()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-south", true, ["miami"]),
            ("agent-north", true, ["fort lauderdale"]));

        var hash = ComputePolicyHash(policy);
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // counter=0, service area "orlando" matches neither agent → fall back to all enabled
        var consumption = BuildConsumption(hash, counter: 0);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(consumption);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 1), Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, "orlando", Ct);

        // Falls back to all enabled; counter=0 % 2 = 0 → agent-south
        Assert.Equal("agent-south", decision.AgentId);
    }

    [Fact]
    public async Task RouteLeadAsync_NullServiceArea_RoutesAcrossAllEnabledAgents()
    {
        var policy = BuildPolicy(
            nextLead: null,
            ("agent-a", true, ["miami"]),
            ("agent-b", true, []));

        var hash = ComputePolicyHash(policy);
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // counter=1, no service area → all enabled agents → counter 1 % 2 = 1 → agent-b
        var consumption = BuildConsumption(hash, counter: 1);
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct)).ReturnsAsync(consumption);
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 2), Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);

        Assert.Equal("agent-b", decision.AgentId);
    }

    // ─── CAS conflict on override → fallthrough ───────────────────────────────

    [Fact]
    public async Task RouteLeadAsync_OverrideCasConflict_FallsToRoundRobin()
    {
        var policy = BuildPolicy(
            nextLead: "agent-vip",
            ("agent-vip", true, []),
            ("agent-a", true, []),
            ("agent-b", true, []));

        var hash = ComputePolicyHash(policy);
        _policyStore.Setup(s => s.GetPolicyAsync(AccountId, Ct)).ReturnsAsync(policy);

        // Initial consumption: override not consumed
        var initialConsumption = BuildConsumption(hash, counter: 0, overrideConsumed: false);
        // After CAS conflict, re-read shows override already consumed by concurrent caller
        var consumedByOther = BuildConsumption(hash, counter: 0, overrideConsumed: true);

        var getCallCount = 0;
        _consumptionStore.Setup(s => s.GetAsync(AccountId, Ct))
            .ReturnsAsync(() =>
            {
                getCallCount++;
                return getCallCount == 1 ? initialConsumption : consumedByOther;
            });

        // First SaveIfUnchanged fails (CAS conflict on override mark)
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.OverrideConsumed == true && c.Counter == 0),
                Ct))
            .ReturnsAsync(false);

        // After fallthrough to round-robin, CAS counter increment succeeds
        _consumptionStore.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<BrokerageRoutingConsumption>(c => c.Counter == 1),
                Ct))
            .ReturnsAsync(true);

        var decision = await _sut.RouteLeadAsync(AccountId, LeadId, null, Ct);

        Assert.Equal("round-robin", decision.Reason);
    }

    // ─── SHA-256 helper (mirrors service's ComputeSha256 for test data setup) ──

    private static string ComputePolicyHash(RoutingPolicy policy)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(policy);
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }
}
