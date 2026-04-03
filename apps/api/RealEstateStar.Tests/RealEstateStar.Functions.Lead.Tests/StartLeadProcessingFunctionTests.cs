using FluentAssertions;
using DomainLead = RealEstateStar.Domain.Leads.Models.Lead;
using LeadType = RealEstateStar.Domain.Leads.Models.LeadType;
using LeadStatus = RealEstateStar.Domain.Leads.Models.LeadStatus;
using SellerDetails = RealEstateStar.Domain.Leads.Models.SellerDetails;
using BuyerDetails = RealEstateStar.Domain.Leads.Models.BuyerDetails;

namespace RealEstateStar.Functions.Lead.Tests;

/// <summary>
/// Tests for the content hash helpers in <see cref="StartLeadProcessingFunction"/>.
/// These hashes are used for cross-lead cache dedup and must be stable across restarts.
/// </summary>
public class StartLeadProcessingFunctionTests
{
    // ── ComputeCmaInputHash ───────────────────────────────────────────────────

    [Fact]
    public void ComputeCmaInputHash_returns_consistent_hash_for_same_address()
    {
        var lead = BuildSellerLead("123 Main St", "Princeton", "NJ", "08540");

        var hash1 = StartLeadProcessingFunction.ComputeCmaInputHash(lead);
        var hash2 = StartLeadProcessingFunction.ComputeCmaInputHash(lead);

        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ComputeCmaInputHash_differs_for_different_addresses()
    {
        var lead1 = BuildSellerLead("123 Main St", "Princeton", "NJ", "08540");
        var lead2 = BuildSellerLead("456 Oak Ave", "Princeton", "NJ", "08540");

        var hash1 = StartLeadProcessingFunction.ComputeCmaInputHash(lead1);
        var hash2 = StartLeadProcessingFunction.ComputeCmaInputHash(lead2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeCmaInputHash_differs_for_different_cities()
    {
        var lead1 = BuildSellerLead("123 Main St", "Princeton", "NJ", "08540");
        var lead2 = BuildSellerLead("123 Main St", "Trenton", "NJ", "08601");

        var hash1 = StartLeadProcessingFunction.ComputeCmaInputHash(lead1);
        var hash2 = StartLeadProcessingFunction.ComputeCmaInputHash(lead2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeCmaInputHash_returns_value_when_seller_details_null()
    {
        var lead = BuildBuyerLead("Princeton", "NJ");

        // CMA hash for a buyer (no seller details) — should return empty/null-safe hash
        var hash = StartLeadProcessingFunction.ComputeCmaInputHash(lead);
        hash.Should().NotBeNullOrEmpty();
    }

    // ── ComputeHsInputHash ────────────────────────────────────────────────────

    [Fact]
    public void ComputeHsInputHash_returns_consistent_hash_for_same_criteria()
    {
        var lead = BuildBuyerLead("Princeton", "NJ", 300000m, 500000m, 3, 2);

        var hash1 = StartLeadProcessingFunction.ComputeHsInputHash(lead);
        var hash2 = StartLeadProcessingFunction.ComputeHsInputHash(lead);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHsInputHash_differs_for_different_budgets()
    {
        var lead1 = BuildBuyerLead("Princeton", "NJ", 300000m, 500000m);
        var lead2 = BuildBuyerLead("Princeton", "NJ", 400000m, 600000m);

        var hash1 = StartLeadProcessingFunction.ComputeHsInputHash(lead1);
        var hash2 = StartLeadProcessingFunction.ComputeHsInputHash(lead2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHsInputHash_differs_for_different_bedroom_count()
    {
        var lead1 = BuildBuyerLead("Princeton", "NJ", 300000m, 500000m, 2);
        var lead2 = BuildBuyerLead("Princeton", "NJ", 300000m, 500000m, 4);

        var hash1 = StartLeadProcessingFunction.ComputeHsInputHash(lead1);
        var hash2 = StartLeadProcessingFunction.ComputeHsInputHash(lead2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHsInputHash_returns_value_when_buyer_details_null()
    {
        var lead = BuildSellerLead("123 Main", "Princeton", "NJ", "08540");
        var hash = StartLeadProcessingFunction.ComputeHsInputHash(lead);
        hash.Should().NotBeNullOrEmpty();
    }

    // ── Routing flag logic — verifies ShouldRunCma/ShouldRunHomeSearch conditions ──

    [Fact]
    public void Seller_lead_with_seller_details_should_run_cma()
    {
        var lead = BuildSellerLead("123 Main", "Princeton", "NJ", "08540");
        var shouldRunCma = lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null;
        var shouldRunHs = lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null;

        shouldRunCma.Should().BeTrue();
        shouldRunHs.Should().BeFalse();
    }

    [Fact]
    public void Buyer_lead_with_buyer_details_should_run_home_search()
    {
        var lead = BuildBuyerLead("Princeton", "NJ");
        var shouldRunCma = lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null;
        var shouldRunHs = lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null;

        shouldRunCma.Should().BeFalse();
        shouldRunHs.Should().BeTrue();
    }

    [Fact]
    public void Both_lead_with_both_details_should_run_cma_and_home_search()
    {
        var lead = BuildBothLead("123 Main", "Princeton", "NJ", "08540");
        var shouldRunCma = lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null;
        var shouldRunHs = lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null;

        shouldRunCma.Should().BeTrue();
        shouldRunHs.Should().BeTrue();
    }

    [Fact]
    public void Seller_lead_without_seller_details_should_not_run_cma()
    {
        var lead = new DomainLead
        {
            Id = Guid.NewGuid(),
            AgentId = "agent-1",
            LeadType = LeadType.Seller,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "555-1234",
            Timeline = "3 months",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow
        };

        var shouldRunCma = lead.LeadType is LeadType.Seller or LeadType.Both && lead.SellerDetails is not null;
        shouldRunCma.Should().BeFalse();
    }

    // ── Instance ID format ─────────────────────────────────────────────────────

    [Fact]
    public void InstanceId_format_is_deterministic()
    {
        const string agentId = "agent-123";
        const string leadId = "lead-456";

        var instanceId = $"lead-{agentId}-{leadId}";
        instanceId.Should().Be("lead-agent-123-lead-456");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DomainLead BuildSellerLead(string address, string city, string state, string zip) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = "agent-1",
            LeadType = LeadType.Seller,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            Phone = "555-5678",
            Timeline = "ASAP",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = new SellerDetails
            {
                Address = address,
                City = city,
                State = state,
                Zip = zip
            }
        };

    private static DomainLead BuildBuyerLead(string city, string state,
        decimal? minBudget = null, decimal? maxBudget = null,
        int? bedrooms = null, int? bathrooms = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = "agent-1",
            LeadType = LeadType.Buyer,
            FirstName = "Bob",
            LastName = "Jones",
            Email = "bob@example.com",
            Phone = "555-9012",
            Timeline = "6 months",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            BuyerDetails = new BuyerDetails
            {
                City = city,
                State = state,
                MinBudget = minBudget,
                MaxBudget = maxBudget,
                Bedrooms = bedrooms,
                Bathrooms = bathrooms
            }
        };

    private static DomainLead BuildBothLead(string address, string city, string state, string zip) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = "agent-1",
            LeadType = LeadType.Both,
            FirstName = "Alice",
            LastName = "Brown",
            Email = "alice@example.com",
            Phone = "555-3456",
            Timeline = "Flexible",
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = new SellerDetails { Address = address, City = city, State = state, Zip = zip },
            BuyerDetails = new BuyerDetails { City = city, State = state }
        };
}
