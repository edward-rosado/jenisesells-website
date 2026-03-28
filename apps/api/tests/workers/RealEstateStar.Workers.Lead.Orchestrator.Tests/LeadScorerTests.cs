using FluentAssertions;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Lead.Orchestrator;

using DomainLead = global::RealEstateStar.Domain.Leads.Models.Lead;

namespace RealEstateStar.Workers.Lead.Orchestrator.Tests;

public class LeadScorerTests
{
    private readonly ILeadScorer _scorer = new LeadScorer();

    private static DomainLead MakeLead(
        string agentId = "test-agent",
        LeadType leadType = LeadType.Buyer,
        string timeline = "asap",
        SellerDetails? sellerDetails = null,
        BuyerDetails? buyerDetails = null,
        string? notes = null,
        int submissionCount = 1)
    {
        return new DomainLead
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LeadType = leadType,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "555-1234",
            Timeline = timeline,
            Status = LeadStatus.Received,
            ReceivedAt = DateTime.UtcNow,
            SellerDetails = sellerDetails,
            BuyerDetails = buyerDetails,
            Notes = notes,
            SubmissionCount = submissionCount
        };
    }

    // ── Timeline scoring ─────────────────────────────────────────────────────

    [Fact]
    public void Score_TimelineScores_AreCorrect()
    {
        var testCases = new[]
        {
            ("asap", 100),
            ("1-3months", 80),
            ("3-6months", 50),
            ("6-12months", 25),
            ("justcurious", 10),
            ("unknown-value", 10)
        };

        foreach (var (timeline, expected) in testCases)
        {
            var lead = MakeLead(timeline: timeline, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
            var score = _scorer.Score(lead);
            score.Factors.Single(f => f.Category == "Timeline").Score
                .Should().Be(expected, $"timeline '{timeline}' should score {expected}");
        }
    }

    // ── Notes scoring ────────────────────────────────────────────────────────

    [Fact]
    public void Score_NotesProvided_ScoresHigherThanNoNotes()
    {
        var withNotes = MakeLead(notes: "Very motivated buyer");
        var withoutNotes = MakeLead(notes: null);

        var scoreWith = _scorer.Score(withNotes);
        var scoreWithout = _scorer.Score(withoutNotes);

        scoreWith.OverallScore.Should().BeGreaterThan(scoreWithout.OverallScore);
    }

    // ── Engagement (SubmissionCount) scoring ─────────────────────────────────

    [Fact]
    public void Score_EngagementFactor_IsAlwaysPresent()
    {
        var lead = MakeLead(submissionCount: 1, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
        var score = _scorer.Score(lead);

        score.Factors.Should().ContainSingle(f => f.Category == "Engagement");
    }

    [Fact]
    public void Score_EngagementFactor_SubmissionCount1_ScoresZero()
    {
        var lead = MakeLead(submissionCount: 1, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "Engagement").Score.Should().Be(0);
    }

    [Fact]
    public void Score_EngagementFactor_SubmissionCount2_Scores50()
    {
        var lead = MakeLead(submissionCount: 2, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "Engagement").Score.Should().Be(50);
    }

    [Fact]
    public void Score_EngagementFactor_SubmissionCount3_Scores80()
    {
        var lead = MakeLead(submissionCount: 3, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "Engagement").Score.Should().Be(80);
    }

    [Fact]
    public void Score_EngagementFactor_SubmissionCount4OrMore_Scores100()
    {
        foreach (var count in new[] { 4, 5, 10 })
        {
            var lead = MakeLead(submissionCount: count, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
            var score = _scorer.Score(lead);

            score.Factors.Single(f => f.Category == "Engagement").Score
                .Should().Be(100, $"submission count {count} should score 100");
        }
    }

    [Fact]
    public void Score_EngagementFactor_Weight_IsPoint10()
    {
        var lead = MakeLead(submissionCount: 2, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "Engagement").Weight.Should().Be(0.10m);
    }

    [Fact]
    public void Score_EngagementFactor_ExplanationMentionsSubmissionCount()
    {
        var lead = MakeLead(submissionCount: 3, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "Engagement").Explanation
            .Should().Contain("3");
    }

    [Fact]
    public void Score_RepeatSubmitter_ScoresHigherThanFirstTimeSubmitter()
    {
        var firstTime = MakeLead(submissionCount: 1, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });
        var repeat = MakeLead(submissionCount: 4, buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });

        var firstScore = _scorer.Score(firstTime);
        var repeatScore = _scorer.Score(repeat);

        repeatScore.OverallScore.Should().BeGreaterThan(firstScore.OverallScore);
    }

    // ── Seller-specific ─────────────────────────────────────────────────────

    [Fact]
    public void Score_SellerWithAsapTimelineAndFullDetails_ReturnsHotScore()
    {
        var lead = MakeLead(
            leadType: LeadType.Seller,
            timeline: "asap",
            sellerDetails: new SellerDetails
            {
                Address = "123 Main St", City = "Springfield", State = "IL", Zip = "62701",
                Beds = 3, Baths = 2, Sqft = 2000, PropertyType = "Single Family", Condition = "Good"
            });

        var score = _scorer.Score(lead);

        score.OverallScore.Should().BeGreaterThanOrEqualTo(80);
        score.Explanation.Should().Contain("Hot");
        score.Explanation.Should().Contain("seller");
    }

    [Fact]
    public void Score_SellerOnlyPropertyDetailsWeight_Is0Point25()
    {
        var lead = MakeLead(
            leadType: LeadType.Seller,
            sellerDetails: new SellerDetails { Address = "1 A St", City = "C", State = "NJ", Zip = "07001", Beds = 3, Baths = 2, Sqft = 1500 });

        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "PropertyDetails").Weight.Should().Be(0.25m);
    }

    // ── Buyer-specific ───────────────────────────────────────────────────────

    [Fact]
    public void Score_BuyerWithJustCuriousAndNoBudget_ReturnsCoolScore()
    {
        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "justcurious",
            buyerDetails: new BuyerDetails { City = "Springfield", State = "IL" });

        var score = _scorer.Score(lead);

        score.OverallScore.Should().BeLessThan(40);
        score.Explanation.Should().Contain("Cool");
    }

    [Fact]
    public void Score_BuyerPreApprovalScores_AreCorrect()
    {
        var testCases = new[] { ("yes", 100), ("in-progress", 60), (null, 20), ("no", 20) };

        foreach (var (status, expected) in testCases)
        {
            var lead = MakeLead(
                leadType: LeadType.Buyer,
                buyerDetails: new BuyerDetails { City = "Springfield", State = "IL", PreApproved = status });

            var score = _scorer.Score(lead);
            score.Factors.Single(f => f.Category == "PreApproval").Score
                .Should().Be(expected, $"pre-approval '{status}' should score {expected}");
        }
    }

    [Fact]
    public void Score_BuyerOnlyPreApprovalWeight_Is0Point25()
    {
        var lead = MakeLead(
            leadType: LeadType.Buyer,
            buyerDetails: new BuyerDetails { City = "Springfield", State = "IL", PreApproved = "yes" });

        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "PreApproval").Weight.Should().Be(0.25m);
    }

    // ── Both buyer + seller ──────────────────────────────────────────────────

    [Fact]
    public void Score_BothBuyerSeller_HasAllFactorsIncludingEngagement()
    {
        var lead = MakeLead(
            leadType: LeadType.Both,
            timeline: "1-3months",
            sellerDetails: new SellerDetails { Address = "1 Main", City = "C", State = "NJ", Zip = "07001", Beds = 3, Baths = 2, Sqft = 2000 },
            buyerDetails: new BuyerDetails { City = "C", State = "NJ", MinBudget = 200000m, MaxBudget = 300000m, PreApproved = "yes" },
            notes: "Motivated",
            submissionCount: 2);

        var score = _scorer.Score(lead);

        // Timeline + Notes + Engagement + PropertyDetails + PreApproval + BudgetAlignment = 6 factors
        score.Factors.Count.Should().Be(6);
        score.Factors.Should().ContainSingle(f => f.Category == "Timeline");
        score.Factors.Should().ContainSingle(f => f.Category == "Notes");
        score.Factors.Should().ContainSingle(f => f.Category == "Engagement");
        score.Factors.Should().ContainSingle(f => f.Category == "PropertyDetails");
        score.Factors.Should().ContainSingle(f => f.Category == "PreApproval");
        score.Factors.Should().ContainSingle(f => f.Category == "BudgetAlignment");
    }

    [Fact]
    public void Score_BothBuyerSeller_PropertyDetailsAndPreApprovalWeight_Is0Point15Each()
    {
        var lead = MakeLead(
            leadType: LeadType.Both,
            sellerDetails: new SellerDetails { Address = "2 Main", City = "C", State = "NJ", Zip = "07001", Beds = 3, Baths = 2, Sqft = 2000 },
            buyerDetails: new BuyerDetails { City = "C", State = "NJ", MinBudget = 200000m, MaxBudget = 300000m });

        var score = _scorer.Score(lead);

        score.Factors.Single(f => f.Category == "PropertyDetails").Weight.Should().Be(0.15m);
        score.Factors.Single(f => f.Category == "PreApproval").Weight.Should().Be(0.15m);
    }

    // ── Normalization ─────────────────────────────────────────────────────────

    [Fact]
    public void Score_OverallScore_IsNormalizedBy_TotalWeight()
    {
        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "asap",
            buyerDetails: new BuyerDetails { City = "Springfield", State = "IL", MinBudget = 300000m, MaxBudget = 400000m, PreApproved = "yes" });

        var score = _scorer.Score(lead);

        score.OverallScore.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Score_ExplanationContainsTimelineAndLeadType()
    {
        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "1-3months",
            buyerDetails: new BuyerDetails { City = "Springfield", State = "IL", PreApproved = "yes" });

        var score = _scorer.Score(lead);

        score.Explanation.Should().Contain("1-3months");
        score.Explanation.Should().Contain("buyer");
        score.Explanation.Should().Contain("score");
    }

    [Fact]
    public void Score_AllFactors_HaveNonEmptyExplanations()
    {
        var lead = MakeLead(
            leadType: LeadType.Both,
            timeline: "asap",
            sellerDetails: new SellerDetails { Address = "3 Main", City = "C", State = "NJ", Zip = "07001", Beds = 3, Baths = 2, Sqft = 2000 },
            buyerDetails: new BuyerDetails { City = "C", State = "NJ", MinBudget = 300000m, MaxBudget = 400000m, PreApproved = "yes" },
            notes: "Ready to go");

        var score = _scorer.Score(lead);

        foreach (var factor in score.Factors)
        {
            factor.Explanation.Should().NotBeNullOrWhiteSpace($"{factor.Category} must have an explanation");
        }
    }
}
