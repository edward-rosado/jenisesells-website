using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Workers.Leads;

public class LeadScorer : ILeadScorer
{
    public LeadScore Score(Lead lead)
    {
        var factors = new List<ScoreFactor>();
        var isSeller = lead.SellerDetails is not null;
        var isBuyer = lead.BuyerDetails is not null;

        // Timeline (always 0.35)
        var timelineScore = lead.Timeline switch
        {
            "asap" => 100,
            "1-3months" => 80,
            "3-6months" => 50,
            "6-12months" => 25,
            "justcurious" => 10,
            _ => 10
        };
        factors.Add(new ScoreFactor
        {
            Category = "Timeline",
            Score = timelineScore,
            Weight = 0.35m,
            Explanation = $"Timeline: {lead.Timeline}"
        });

        // Notes (always 0.05)
        var notesScore = string.IsNullOrWhiteSpace(lead.Notes) ? 0 : 100;
        factors.Add(new ScoreFactor
        {
            Category = "Notes",
            Score = notesScore,
            Weight = 0.05m,
            Explanation = notesScore > 0 ? "Additional notes provided" : "No notes"
        });

        // Seller-specific factors
        if (isSeller)
        {
            var s = lead.SellerDetails!;
            var detailScore = (s.Beds.HasValue && s.Baths.HasValue && s.Sqft.HasValue) ? 100
                : (s.Beds.HasValue || s.Baths.HasValue || s.Sqft.HasValue) ? 60 : 40;
            factors.Add(new ScoreFactor
            {
                Category = "PropertyDetails",
                Score = detailScore,
                Weight = isBuyer ? 0.15m : 0.25m,
                Explanation = $"Property detail completeness: {detailScore}%"
            });
        }

        // Buyer-specific factors
        if (isBuyer)
        {
            var b = lead.BuyerDetails!;
            var preApprovalScore = b.PreApproved switch
            {
                "yes" => 100,
                "in-progress" => 60,
                _ => 20
            };
            factors.Add(new ScoreFactor
            {
                Category = "PreApproval",
                Score = preApprovalScore,
                Weight = isSeller ? 0.15m : 0.25m,
                Explanation = $"Pre-approval: {b.PreApproved ?? "none"}"
            });

            var budgetScore = (b.MinBudget.HasValue && b.MaxBudget.HasValue) ? 100
                : (b.MinBudget.HasValue || b.MaxBudget.HasValue) ? 60 : 20;
            factors.Add(new ScoreFactor
            {
                Category = "BudgetAlignment",
                Score = budgetScore,
                Weight = 0.15m,
                Explanation = $"Budget specified: {budgetScore}%"
            });
        }

        // Calculate weighted overall score
        var totalWeight = factors.Sum(f => f.Weight);
        var overall = (int)Math.Round(factors.Sum(f => f.Score * f.Weight) / totalWeight);

        // Determine bucket
        var bucket = overall >= 70 ? "Hot" : overall >= 40 ? "Warm" : "Cool";

        // Determine lead type context
        var type = (isSeller, isBuyer) switch
        {
            (true, true) => "buyer/seller",
            (true, false) => "seller",
            _ => "buyer"
        };

        return new LeadScore
        {
            OverallScore = overall,
            Factors = factors,
            Explanation = $"{bucket} {type} lead — {lead.Timeline} timeline, score {overall}/100"
        };
    }
}
