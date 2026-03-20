using System.Text.Json.Nodes;
using FluentAssertions;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class LeadChatCardRendererTests
{
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
        MotivationAnalysis = "Relocating for a new job opportunity in another city.",
        ProfessionalBackground = "Software engineer, stable income.",
        FinancialIndicators = "Pre-approved for $500k.",
        TimelinePressure = "Needs to move within 90 days.",
        ConversationStarters = ["Ask about the new role."],
        ColdCallOpeners = ["Congratulations on the new opportunity!", "I specialize in relocation buyers.", "Third opener that should not appear"]
    };

    private static LeadScore MakeScore(int overall = 82) => new()
    {
        OverallScore = overall,
        Factors = [],
        Explanation = "Strong motivation and timeline."
    };

    [Fact]
    public void RenderNewLeadCard_ReturnsValidJsonObject()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        card.Should().NotBeNull();
    }

    [Fact]
    public void RenderNewLeadCard_HasCardsV2Array()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var cardsV2 = card["cardsV2"]?.AsArray();
        cardsV2.Should().NotBeNull();
        cardsV2!.Count.Should().Be(1);
    }

    [Fact]
    public void RenderNewLeadCard_HeaderContainsLeadName()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var header = card["cardsV2"]![0]!["card"]!["header"];
        header!["title"]!.GetValue<string>().Should().Contain("Jane Doe");
    }

    [Fact]
    public void RenderNewLeadCard_HeaderContainsScore()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore(82));
        var subtitle = card["cardsV2"]![0]!["card"]!["header"]!["subtitle"]!.GetValue<string>();
        subtitle.Should().Contain("82");
    }

    [Fact]
    public void RenderNewLeadCard_HeaderContainsLeadTypes()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var subtitle = card["cardsV2"]![0]!["card"]!["header"]!["subtitle"]!.GetValue<string>();
        subtitle.Should().Contain("buying").And.Contain("selling");
    }

    [Fact]
    public void RenderNewLeadCard_HasFourSections()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var sections = card["cardsV2"]![0]!["card"]!["sections"]!.AsArray();
        sections.Count.Should().Be(4);
    }

    [Fact]
    public void RenderNewLeadCard_ContactSectionContainsEmailAndPhone()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var contactSection = card["cardsV2"]![0]!["card"]!["sections"]![0]!.ToJsonString();
        contactSection.Should().Contain("jane@example.com");
        contactSection.Should().Contain("5551234567");
    }

    [Fact]
    public void RenderNewLeadCard_MotivationSectionContainsAnalysisSnippet()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var motivationSection = card["cardsV2"]![0]!["card"]!["sections"]![1]!.ToJsonString();
        motivationSection.Should().Contain("Relocating for a new job");
    }

    [Fact]
    public void RenderNewLeadCard_ColdCallOpenersSection_ContainsFirstTwoOpeners()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var openersSection = card["cardsV2"]![0]!["card"]!["sections"]![2]!.ToJsonString();
        openersSection.Should().Contain("Congratulations on the new opportunity!");
        openersSection.Should().Contain("I specialize in relocation buyers.");
        openersSection.Should().NotContain("Third opener that should not appear");
    }

    [Fact]
    public void RenderNewLeadCard_ButtonSectionHasViewInDriveButton()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var buttonSection = card["cardsV2"]![0]!["card"]!["sections"]![3]!.ToJsonString();
        buttonSection.Should().Contain("View in Drive");
    }

    [Fact]
    public void RenderNewLeadCard_ButtonUrl_ContainsLeadName()
    {
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), MakeEnrichment(), MakeScore());
        var buttonSection = card["cardsV2"]![0]!["card"]!["sections"]![3]!.ToJsonString();
        // URL-encoded "Jane Doe" should appear in the button URL
        buttonSection.Should().Contain("Jane");
    }

    [Fact]
    public void RenderNewLeadCard_LongMotivationAnalysis_IsTruncatedTo200Chars()
    {
        var longAnalysis = new string('A', 250);
        var enrichment = MakeEnrichment() with { MotivationAnalysis = longAnalysis };
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), enrichment, MakeScore());
        var motivationSection = card["cardsV2"]![0]!["card"]!["sections"]![1]!.ToJsonString();
        // Should contain ellipsis indicating truncation (JSON-escaped or literal)
        motivationSection.Should().Match(s => s.Contains("…") || s.Contains(@"\u2026"));
        // Should not contain more than 200 A's
        motivationSection.Should().NotContain(new string('A', 201));
    }

    [Fact]
    public void RenderNewLeadCard_WhenNoColdCallOpeners_EmptyWidgets()
    {
        var enrichment = MakeEnrichment() with { ColdCallOpeners = [] };
        var card = LeadChatCardRenderer.RenderNewLeadCard(MakeLead(), enrichment, MakeScore());
        var openersSection = card["cardsV2"]![0]!["card"]!["sections"]![2]!;
        var widgets = openersSection["widgets"]!.AsArray();
        widgets.Count.Should().Be(0);
    }
}
