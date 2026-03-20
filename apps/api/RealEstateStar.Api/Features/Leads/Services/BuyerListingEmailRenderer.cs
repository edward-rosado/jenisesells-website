using System.Text;
using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Features.Leads.Services;

public static class BuyerListingEmailRenderer
{
    public static (string Subject, string Body) Render(
        string buyerFirstName,
        IEnumerable<Listing> listings,
        AccountConfig config,
        LeadEnrichment? enrichment = null)
    {
        var listingList = listings.ToList();
        var count = listingList.Count;

        var subject = $"{count} {(count == 1 ? "Home" : "Homes")} Curated Just for You, {buyerFirstName}!";
        var body = BuildBody(buyerFirstName, listingList, config, enrichment);

        return (subject, body);
    }

    private static string BuildBody(
        string buyerFirstName,
        List<Listing> listings,
        AccountConfig config,
        LeadEnrichment? enrichment)
    {
        var agentName    = config.Agent?.Name ?? "Your Real Estate Professional";
        var agentPhone   = config.Agent?.Phone ?? "";
        var agentEmail   = config.Agent?.Email ?? "";
        var brokerage    = config.Brokerage?.Name ?? "";
        var officeAddress = config.Brokerage?.OfficeAddress;
        if (string.IsNullOrWhiteSpace(officeAddress))
        {
            officeAddress = config.Brokerage?.Name ?? "Address not available";
        }

        var sb = new StringBuilder();

        // Personalized intro
        sb.AppendLine($"Hi {buyerFirstName},");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(enrichment?.MotivationAnalysis)
            && !string.Equals(enrichment.MotivationAnalysis, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"Based on what you've shared, {enrichment.MotivationAnalysis.TrimEnd('.')}.");
            sb.AppendLine();
        }

        sb.AppendLine($"I've personally curated {listings.Count} {(listings.Count == 1 ? "home" : "homes")} that match what you're looking for. Take a look and let me know which ones catch your eye!");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Listing cards
        for (var i = 0; i < listings.Count; i++)
        {
            var l    = listings[i];
            var sqft = l.Sqft.HasValue ? $" / {l.Sqft.Value:N0} sqft" : "";

            sb.AppendLine($"**{i + 1}. {l.Address}**");
            sb.AppendLine($"{l.City}, {l.State} {l.Zip}");
            sb.AppendLine($"${l.Price:N0} • {l.Beds} bed / {l.Baths} bath{sqft}");

            if (!string.IsNullOrWhiteSpace(l.WhyThisFits))
            {
                sb.AppendLine();
                sb.AppendLine($"Why this fits: {l.WhyThisFits}");
            }

            if (!string.IsNullOrWhiteSpace(l.ListingUrl))
            {
                sb.AppendLine();
                sb.AppendLine($"View listing: {l.ListingUrl}");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Agent sign-off
        sb.AppendLine("I'd love to schedule a showing for any of these homes. Reply to this email or give me a call anytime!");
        sb.AppendLine();
        sb.AppendLine($"Warm regards,");
        sb.AppendLine($"{agentName}");
        if (!string.IsNullOrWhiteSpace(brokerage))    sb.AppendLine(brokerage);
        if (!string.IsNullOrWhiteSpace(agentPhone))   sb.AppendLine(agentPhone);
        if (!string.IsNullOrWhiteSpace(agentEmail))   sb.AppendLine(agentEmail);

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // CAN-SPAM compliant footer
        sb.AppendLine("You are receiving this email because you inquired about homes in the area.");
        sb.AppendLine($"To manage your email preferences, visit: https://{config.Handle}.real-estate-star.com/privacy/opt-out");
        if (!string.IsNullOrWhiteSpace(officeAddress))
        {
            sb.AppendLine();
            sb.AppendLine(officeAddress);
        }
        if (!string.IsNullOrWhiteSpace(brokerage))
        {
            sb.AppendLine(brokerage);
        }

        return sb.ToString();
    }
}
