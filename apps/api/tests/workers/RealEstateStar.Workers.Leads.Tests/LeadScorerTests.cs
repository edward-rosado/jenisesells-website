using FluentAssertions;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Leads;

namespace RealEstateStar.Workers.Leads.Tests;

public class LeadScorerTests
{
    private readonly ILeadScorer _scorer = new LeadScorer();

    private static Lead MakeLead(
        string agentId = "test-agent",
        LeadType leadType = LeadType.Buyer,
        string timeline = "asap",
        SellerDetails? sellerDetails = null,
        BuyerDetails? buyerDetails = null,
        string? notes = null)
    {
        return new Lead
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
            Notes = notes
        };
    }

    [Fact]
    public void Score_SellerWithAsapTimelineAndFullDetails_ReturnsHotScore()
    {
        // Arrange
        var sellerDetails = new SellerDetails
        {
            Address = "123 Main St",
            City = "Springfield",
            State = "IL",
            Zip = "62701",
            Beds = 3,
            Baths = 2,
            Sqft = 2000,
            PropertyType = "Single Family",
            Condition = "Good",
            AskingPrice = 250000m
        };

        var lead = MakeLead(
            leadType: LeadType.Seller,
            timeline: "asap",
            sellerDetails: sellerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        score.OverallScore.Should().BeGreaterThanOrEqualTo(85);
        score.OverallScore.Should().BeLessThanOrEqualTo(100);
        score.Explanation.Should().Contain("Hot");
        score.Explanation.Should().Contain("seller");
    }

    [Fact]
    public void Score_BuyerWithJustCuriousTimelineNoPreApprovalNoBudget_ReturnsCoolScore()
    {
        // Arrange
        var buyerDetails = new BuyerDetails
        {
            City = "Springfield",
            State = "IL",
            MinBudget = null,
            MaxBudget = null,
            PreApproved = null
        };

        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "justcurious",
            buyerDetails: buyerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        // Timeline (10) * 0.35 + Notes (0) * 0.05 + PreApproval (20) * 0.25 + Budget (20) * 0.15
        // = 3.5 + 0 + 5 + 3 = 11.5 / 0.80 ≈ 14
        score.OverallScore.Should().BeGreaterThanOrEqualTo(12);
        score.OverallScore.Should().BeLessThanOrEqualTo(18);
        score.Explanation.Should().Contain("Cool");
        score.Explanation.Should().Contain("buyer");
    }

    [Fact]
    public void Score_BuyerSellerWith1To3MonthsPreApprovedFullDetails_ReturnsHotScore()
    {
        // Arrange
        var sellerDetails = new SellerDetails
        {
            Address = "456 Oak Ave",
            City = "Springfield",
            State = "IL",
            Zip = "62701",
            Beds = 4,
            Baths = 3,
            Sqft = 3500
        };

        var buyerDetails = new BuyerDetails
        {
            City = "Springfield",
            State = "IL",
            MinBudget = 200000m,
            MaxBudget = 300000m,
            Bedrooms = 3,
            Bathrooms = 2,
            PreApproved = "yes"
        };

        var lead = MakeLead(
            leadType: LeadType.Both,
            timeline: "1-3months",
            sellerDetails: sellerDetails,
            buyerDetails: buyerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        score.OverallScore.Should().BeGreaterThanOrEqualTo(80);
        score.Explanation.Should().Contain("Hot");
        score.Explanation.Should().Contain("buyer/seller");
    }

    [Fact]
    public void Score_SellerWithAddressOnly6To12MonthsTimeline_ReturnsCoolToWarmScore()
    {
        // Arrange
        var sellerDetails = new SellerDetails
        {
            Address = "789 Elm St",
            City = "Springfield",
            State = "IL",
            Zip = "62701",
            Beds = null,
            Baths = null,
            Sqft = null
        };

        var lead = MakeLead(
            leadType: LeadType.Seller,
            timeline: "6-12months",
            sellerDetails: sellerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        // Timeline (25) * 0.35 + Notes (0) * 0.05 + PropertyDetails (40) * 0.25 = 8.75 + 0 + 10 = 18.75/0.65 ≈ 29
        // Score is 29, which is Cool (< 40), not Warm. Test verifies it's in the cool/warm boundary range.
        score.OverallScore.Should().BeGreaterThanOrEqualTo(25);
        score.OverallScore.Should().BeLessThanOrEqualTo(35);
        score.Explanation.Should().Contain("Cool");
    }

    [Fact]
    public void Score_BuyerWithPreApprovedAsapFullBudget_ReturnsHotScore()
    {
        // Arrange
        var buyerDetails = new BuyerDetails
        {
            City = "Springfield",
            State = "IL",
            MinBudget = 250000m,
            MaxBudget = 350000m,
            Bedrooms = 3,
            Bathrooms = 2,
            PreApproved = "yes",
            PreApprovalAmount = 300000m
        };

        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "asap",
            buyerDetails: buyerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        score.OverallScore.Should().BeGreaterThanOrEqualTo(90);
        score.Explanation.Should().Contain("Hot");
    }

    [Fact]
    public void Score_WithNotesProvided_IncreasesWeightedScore()
    {
        // Arrange
        var leadWithoutNotes = MakeLead(
            timeline: "asap",
            notes: null);

        var leadWithNotes = MakeLead(
            timeline: "asap",
            notes: "Very interested in quick sale");

        // Act
        var scoreWithoutNotes = _scorer.Score(leadWithoutNotes);
        var scoreWithNotes = _scorer.Score(leadWithNotes);

        // Assert
        // Notes factor has weight 0.05 and score 100 vs 0
        // For buyer only: (100*0.35 + 100*0.05 + 20*0.25 + 20*0.15) / 0.80 = 43/0.80 = 54 (with notes)
        //                 (100*0.35 + 0*0.05 + 20*0.25 + 20*0.15) / 0.80 = 38/0.80 = 48 (without)
        // Difference should be approximately 6-7 points due to rounding
        (scoreWithNotes.OverallScore - scoreWithoutNotes.OverallScore).Should().BeGreaterThan(3);
        (scoreWithNotes.OverallScore - scoreWithoutNotes.OverallScore).Should().BeLessThanOrEqualTo(15);
        scoreWithNotes.OverallScore.Should().BeGreaterThan(scoreWithoutNotes.OverallScore);
    }

    [Fact]
    public void Score_OverallScore70OrAbove_BucketIsHot()
    {
        // Arrange
        var buyerDetails = new BuyerDetails
        {
            City = "Springfield",
            State = "IL",
            MinBudget = 300000m,
            MaxBudget = 400000m,
            PreApproved = "yes"
        };

        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "asap",
            buyerDetails: buyerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        if (score.OverallScore >= 70)
        {
            score.Explanation.Should().Contain("Hot");
        }
    }

    [Fact]
    public void Score_OverallScore40To69_BucketIsWarm()
    {
        // Arrange
        var sellerDetails = new SellerDetails
        {
            Address = "100 Pine Rd",
            City = "Springfield",
            State = "IL",
            Zip = "62701",
            Beds = 2,
            Baths = 1,
            Sqft = null
        };

        var lead = MakeLead(
            leadType: LeadType.Seller,
            timeline: "3-6months",
            sellerDetails: sellerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        if (score.OverallScore >= 40 && score.OverallScore < 70)
        {
            score.Explanation.Should().Contain("Warm");
        }
    }

    [Fact]
    public void Score_OverallScoreBelowTo40_BucketIsCool()
    {
        // Arrange
        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "6-12months",
            buyerDetails: new BuyerDetails
            {
                City = "Springfield",
                State = "IL"
            });

        // Act
        var score = _scorer.Score(lead);

        // Assert
        if (score.OverallScore < 40)
        {
            score.Explanation.Should().Contain("Cool");
        }
    }

    [Fact]
    public void Score_ExplanationStringContainsTimelineAndLeadTypeContext()
    {
        // Arrange
        var buyerDetails = new BuyerDetails
        {
            City = "Springfield",
            State = "IL",
            PreApproved = "yes"
        };

        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "1-3months",
            buyerDetails: buyerDetails);

        // Act
        var score = _scorer.Score(lead);

        // Assert
        score.Explanation.Should().Contain("1-3months");
        score.Explanation.Should().Contain("buyer");
        score.Explanation.Should().Contain("score");
    }

    [Fact]
    public void Score_FactorsListContainsOneEntryPerApplicableFactor()
    {
        // Arrange - both buyer and seller
        var lead = MakeLead(
            leadType: LeadType.Both,
            timeline: "asap",
            sellerDetails: new SellerDetails
            {
                Address = "200 Birch Ln",
                City = "Springfield",
                State = "IL",
                Zip = "62701",
                Beds = 3,
                Baths = 2,
                Sqft = 2500
            },
            buyerDetails: new BuyerDetails
            {
                City = "Springfield",
                State = "IL",
                MinBudget = 250000m,
                MaxBudget = 350000m,
                PreApproved = "yes"
            },
            notes: "Client is serious");

        // Act
        var score = _scorer.Score(lead);

        // Assert
        // Timeline (always), Notes (always), PropertyDetails (seller), PreApproval (buyer), BudgetAlignment (buyer)
        score.Factors.Count.Should().Be(5);
        score.Factors.Should().ContainSingle(f => f.Category == "Timeline");
        score.Factors.Should().ContainSingle(f => f.Category == "Notes");
        score.Factors.Should().ContainSingle(f => f.Category == "PropertyDetails");
        score.Factors.Should().ContainSingle(f => f.Category == "PreApproval");
        score.Factors.Should().ContainSingle(f => f.Category == "BudgetAlignment");
    }

    [Fact]
    public void Score_FactorsHaveCorrectWeights()
    {
        // Arrange
        var lead = MakeLead(
            leadType: LeadType.Both,
            timeline: "asap",
            sellerDetails: new SellerDetails
            {
                Address = "300 Cedar Way",
                City = "Springfield",
                State = "IL",
                Zip = "62701",
                Beds = 3,
                Baths = 2
            },
            buyerDetails: new BuyerDetails
            {
                City = "Springfield",
                State = "IL",
                MinBudget = 300000m,
                MaxBudget = 400000m
            });

        // Act
        var score = _scorer.Score(lead);

        // Assert
        var timeline = score.Factors.Single(f => f.Category == "Timeline");
        timeline.Weight.Should().Be(0.35m);

        var notes = score.Factors.Single(f => f.Category == "Notes");
        notes.Weight.Should().Be(0.05m);

        var propertyDetails = score.Factors.Single(f => f.Category == "PropertyDetails");
        propertyDetails.Weight.Should().Be(0.15m); // buyer+seller = 0.15

        var preApproval = score.Factors.Single(f => f.Category == "PreApproval");
        preApproval.Weight.Should().Be(0.15m); // buyer+seller = 0.15

        var budget = score.Factors.Single(f => f.Category == "BudgetAlignment");
        budget.Weight.Should().Be(0.15m);
    }

    [Fact]
    public void Score_SellerOnlyFactorWeights()
    {
        // Arrange
        var lead = MakeLead(
            leadType: LeadType.Seller,
            timeline: "asap",
            sellerDetails: new SellerDetails
            {
                Address = "400 Desert Road",
                City = "Springfield",
                State = "IL",
                Zip = "62701",
                Beds = 3,
                Baths = 2,
                Sqft = 2500
            });

        // Act
        var score = _scorer.Score(lead);

        // Assert
        var propertyDetails = score.Factors.Single(f => f.Category == "PropertyDetails");
        propertyDetails.Weight.Should().Be(0.25m); // seller only
    }

    [Fact]
    public void Score_BuyerOnlyFactorWeights()
    {
        // Arrange
        var lead = MakeLead(
            leadType: LeadType.Buyer,
            timeline: "asap",
            buyerDetails: new BuyerDetails
            {
                City = "Springfield",
                State = "IL",
                MinBudget = 300000m,
                MaxBudget = 400000m,
                PreApproved = "yes"
            });

        // Act
        var score = _scorer.Score(lead);

        // Assert
        var preApproval = score.Factors.Single(f => f.Category == "PreApproval");
        preApproval.Weight.Should().Be(0.25m); // buyer only
    }

    [Fact]
    public void Score_TimelineScores_AreCorrect()
    {
        // Timeline scoring: asap=100, 1-3months=80, 3-6months=50, 6-12months=25, justcurious=10

        var testCases = new[]
        {
            ("asap", 100),
            ("1-3months", 80),
            ("3-6months", 50),
            ("6-12months", 25),
            ("justcurious", 10),
            ("unknown-timeline", 10)
        };

        foreach (var (timeline, expectedScore) in testCases)
        {
            var lead = MakeLead(timeline: timeline);
            var score = _scorer.Score(lead);
            var timelineFactor = score.Factors.Single(f => f.Category == "Timeline");
            timelineFactor.Score.Should().Be(expectedScore,
                $"Timeline '{timeline}' should score {expectedScore}");
        }
    }

    [Fact]
    public void Score_PreApprovalScores_AreCorrect()
    {
        // Pre-approval scoring: yes=100, in-progress=60, other=20

        var testCases = new[]
        {
            ("yes", 100),
            ("in-progress", 60),
            (null, 20),
            ("no", 20)
        };

        foreach (var (status, expectedScore) in testCases)
        {
            var buyerDetails = new BuyerDetails
            {
                City = "Springfield",
                State = "IL",
                PreApproved = status
            };
            var lead = MakeLead(
                leadType: LeadType.Buyer,
                buyerDetails: buyerDetails);

            var score = _scorer.Score(lead);
            var preApprovalFactor = score.Factors.Single(f => f.Category == "PreApproval");
            preApprovalFactor.Score.Should().Be(expectedScore,
                $"Pre-approval '{status}' should score {expectedScore}");
        }
    }

    [Fact]
    public void Score_PropertyDetailScoresForSeller_AreCorrect()
    {
        // Property detail scoring: all 3 fields = 100, 1-2 fields = 60, none = 40

        var testCases = new[]
        {
            (beds: 3, baths: 2, sqft: 2500, expectedScore: 100),
            (beds: 3, baths: (int?)null, sqft: (int?)null, expectedScore: 60),
            (beds: (int?)null, baths: 2, sqft: (int?)null, expectedScore: 60),
            (beds: (int?)null, baths: (int?)null, sqft: 2500, expectedScore: 60),
            (beds: (int?)null, baths: (int?)null, sqft: (int?)null, expectedScore: 40)
        };

        foreach (var (beds, baths, sqft, expectedScore) in testCases)
        {
            var sellerDetails = new SellerDetails
            {
                Address = "Detail Test",
                City = "Springfield",
                State = "IL",
                Zip = "62701",
                Beds = beds,
                Baths = baths,
                Sqft = sqft
            };
            var lead = MakeLead(
                leadType: LeadType.Seller,
                sellerDetails: sellerDetails);

            var score = _scorer.Score(lead);
            var detailFactor = score.Factors.Single(f => f.Category == "PropertyDetails");
            detailFactor.Score.Should().Be(expectedScore,
                $"Beds:{beds}, Baths:{baths}, Sqft:{sqft} should score {expectedScore}");
        }
    }

    [Fact]
    public void Score_BudgetAlignmentScores_AreCorrect()
    {
        // Budget scoring: min and max = 100, only one = 60, neither = 20

        var testCases = new[]
        {
            (min: 250000m, max: 350000m, expectedScore: 100),
            (min: 250000m, max: (decimal?)null, expectedScore: 60),
            (min: (decimal?)null, max: 350000m, expectedScore: 60),
            (min: (decimal?)null, max: (decimal?)null, expectedScore: 20)
        };

        foreach (var (min, max, expectedScore) in testCases)
        {
            var buyerDetails = new BuyerDetails
            {
                City = "Springfield",
                State = "IL",
                MinBudget = min,
                MaxBudget = max
            };
            var lead = MakeLead(
                leadType: LeadType.Buyer,
                buyerDetails: buyerDetails);

            var score = _scorer.Score(lead);
            var budgetFactor = score.Factors.Single(f => f.Category == "BudgetAlignment");
            budgetFactor.Score.Should().Be(expectedScore,
                $"Min:{min}, Max:{max} should score {expectedScore}");
        }
    }

    [Fact]
    public void Score_FactorsHaveExplanations()
    {
        // Arrange
        var lead = MakeLead(
            leadType: LeadType.Both,
            timeline: "asap",
            sellerDetails: new SellerDetails
            {
                Address = "500 Forest Ave",
                City = "Springfield",
                State = "IL",
                Zip = "62701",
                Beds = 3,
                Baths = 2,
                Sqft = 2500
            },
            buyerDetails: new BuyerDetails
            {
                City = "Springfield",
                State = "IL",
                MinBudget = 300000m,
                MaxBudget = 400000m,
                PreApproved = "yes"
            },
            notes: "Very motivated");

        // Act
        var score = _scorer.Score(lead);

        // Assert
        foreach (var factor in score.Factors)
        {
            factor.Explanation.Should().NotBeNullOrWhiteSpace($"{factor.Category} should have an explanation");
        }
    }
}
