using RealEstateStar.Domain.Leads.Models;
using System.Globalization;
using System.Text;

namespace RealEstateStar.Domain.HomeSearch.Markdown;

public static class HomeSearchMarkdownRenderer
{
    private static readonly CultureInfo UsFormat = new("en-US");

    private static string FormatUsd(decimal value) =>
        $"${value.ToString("N0", UsFormat)}";
    public static string RenderListings(Lead lead, List<Listing> listings, string agentName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"leadName: {lead.FullName}");
        sb.AppendLine($"generatedAt: {DateTime.UtcNow:o}");
        sb.AppendLine($"listingCount: {listings.Count}");
        sb.AppendLine($"agent: {agentName}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# Home Search Results for {lead.FullName}");
        sb.AppendLine();
        sb.AppendLine($"**Search criteria:** {lead.BuyerDetails?.City}, {lead.BuyerDetails?.State}");

        if (lead.BuyerDetails?.MinBudget.HasValue == true || lead.BuyerDetails?.MaxBudget.HasValue == true)
        {
            var min = lead.BuyerDetails.MinBudget.HasValue ? FormatUsd(lead.BuyerDetails.MinBudget.Value) : "any";
            var max = lead.BuyerDetails.MaxBudget.HasValue ? FormatUsd(lead.BuyerDetails.MaxBudget.Value) : "any";
            sb.AppendLine($"**Price range:** {min} – {max}");
        }

        if (lead.BuyerDetails?.Bedrooms.HasValue == true)
            sb.AppendLine($"**Bedrooms:** {lead.BuyerDetails.Bedrooms}+");

        if (lead.BuyerDetails?.Bathrooms.HasValue == true)
            sb.AppendLine($"**Bathrooms:** {lead.BuyerDetails.Bathrooms}+");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var (listing, i) in listings.Select((l, i) => (l, i)))
        {
            sb.AppendLine($"## {i + 1}. {listing.Address}, {listing.City}, {listing.State} {listing.Zip}");
            sb.AppendLine();
            sb.AppendLine($"- **Price:** {FormatUsd(listing.Price)}");
            sb.AppendLine($"- **Beds/Baths:** {listing.Beds} / {listing.Baths}");

            if (listing.Sqft.HasValue)
                sb.AppendLine($"- **Sqft:** {listing.Sqft:N0}");

            if (listing.WhyThisFits is not null)
                sb.AppendLine($"- **Why this fits:** {listing.WhyThisFits}");

            if (listing.ListingUrl is not null)
                sb.AppendLine($"- **Listing:** {listing.ListingUrl}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string RenderEmailBody(Lead lead, List<Listing> listings, string agentName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {lead.FirstName},");
        sb.AppendLine();
        sb.AppendLine($"Thank you for reaching out! Based on what you're looking for — a home in {lead.BuyerDetails?.City}, {lead.BuyerDetails?.State} — I've found {listings.Count} listings I think you'll love.");
        sb.AppendLine();

        foreach (var listing in listings)
        {
            sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();
            sb.AppendLine($"📍 {listing.Address}, {listing.City}, {listing.State} {listing.Zip}");
            sb.AppendLine($"   {FormatUsd(listing.Price)} | {listing.Beds} bed | {listing.Baths} bath{(listing.Sqft.HasValue ? $" | {listing.Sqft.Value.ToString("N0", UsFormat)} sqft" : "")}");

            if (listing.WhyThisFits is not null)
                sb.AppendLine($"   → \"{listing.WhyThisFits}\"");

            if (listing.ListingUrl is not null)
                sb.AppendLine($"   View: {listing.ListingUrl}");

            sb.AppendLine();
        }

        sb.AppendLine("Want to schedule a tour or see more options? Just reply to this email.");
        sb.AppendLine();
        sb.AppendLine($"Best,");
        sb.AppendLine(agentName);

        return sb.ToString();
    }
}
