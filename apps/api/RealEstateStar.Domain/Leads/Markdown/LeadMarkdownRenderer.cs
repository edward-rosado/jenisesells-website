using RealEstateStar.Domain.Leads.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace RealEstateStar.Domain.Leads.Markdown;

public static partial class LeadMarkdownRenderer
{
    // ─── Public API ──────────────────────────────────────────────────────────────

    public static string RenderLeadProfile(Lead lead)
    {
        var sb = new StringBuilder();
        var city  = lead.SellerDetails?.City  ?? lead.BuyerDetails?.City  ?? "";
        var state = lead.SellerDetails?.State ?? lead.BuyerDetails?.State ?? "";
        var tags  = BuildTags(lead);

        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine("# === System ===");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"status: {lead.Status}");
        sb.AppendLine($"submissionCount: {lead.SubmissionCount}");
        sb.AppendLine($"receivedAt: {lead.ReceivedAt:O}");
        sb.AppendLine($"homeSearchId: {lead.HomeSearchId?.ToString() ?? ""}");
        sb.AppendLine($"consent_token_hash: {lead.ConsentTokenHash ?? ""}");
        sb.AppendLine();
        sb.AppendLine("# === Indexable ===");
        sb.AppendLine($"firstName: \"{EscapeYaml(lead.FirstName)}\"");
        sb.AppendLine($"lastName: \"{EscapeYaml(lead.LastName)}\"");
        sb.AppendLine($"email: \"{EscapeYaml(lead.Email)}\"");
        sb.AppendLine($"phone: \"{EscapeYaml(lead.Phone)}\"");
        sb.AppendLine($"leadType: {lead.LeadType}");
        sb.AppendLine($"timeline: \"{EscapeYaml(lead.Timeline)}\"");
        sb.AppendLine($"city: \"{EscapeYaml(city)}\"");
        sb.AppendLine($"state: \"{EscapeYaml(state)}\"");
        sb.AppendLine($"tags: [{string.Join(", ", tags)}]");
        sb.AppendLine("---");
        sb.AppendLine();

        // Heading
        sb.AppendLine($"# {lead.FullName}");
        sb.AppendLine();
        sb.AppendLine($"**Received {lead.ReceivedAt:MMMM d, yyyy} at {lead.ReceivedAt:h:mm tt}**");
        sb.AppendLine();

        // Contact
        sb.AppendLine("## Contact");
        sb.AppendLine($"- **Phone:** {FormatPhone(lead.Phone)}");
        sb.AppendLine($"- **Email:** {lead.Email}");
        sb.AppendLine($"- **Timeline:** {FormatTimeline(lead.Timeline)}");
        sb.AppendLine();

        // Selling
        if (lead.SellerDetails is { } s)
        {
            sb.AppendLine("## Selling");
            var addressLine = string.IsNullOrWhiteSpace(s.Zip)
                ? $"{s.Address}, {s.City}, {s.State}"
                : $"{s.Address}, {s.City}, {s.State} {s.Zip}";
            sb.AppendLine($"- **Address:** {addressLine}");
            if (s.PropertyType is not null) sb.AppendLine($"- **Property Type:** {s.PropertyType}");
            if (s.Condition    is not null) sb.AppendLine($"- **Condition:** {s.Condition}");
            if (s.AskingPrice  is not null) sb.AppendLine($"- **Asking Price:** {FormatCurrency(s.AskingPrice.Value)}");
            sb.AppendLine();
        }

        // Buying
        if (lead.BuyerDetails is { } b)
        {
            sb.AppendLine("## Buying");
            sb.AppendLine($"- **Desired Area:** {b.City}, {b.State}");
            if (b.MaxBudget    is not null) sb.AppendLine($"- **Max Budget:** {FormatCurrency(b.MaxBudget.Value)}");
            if (b.Bedrooms     is not null) sb.AppendLine($"- **Bedrooms:** {b.Bedrooms}");
            if (b.Bathrooms    is not null) sb.AppendLine($"- **Bathrooms:** {b.Bathrooms}");
            if (b.PropertyTypes is { Count: > 0 }) sb.AppendLine($"- **Property Types:** {string.Join(", ", b.PropertyTypes)}");
            if (b.MustHaves    is { Count: > 0 }) sb.AppendLine($"- **Must-Haves:** {string.Join(", ", b.MustHaves)}");
            sb.AppendLine();
        }

        // Notes
        if (lead.Notes is not null)
        {
            sb.AppendLine("## Notes");
            sb.AppendLine(lead.Notes);
        }

        return sb.ToString();
    }

    // TODO: Pipeline redesign — LeadEnrichment removed in Phase 1.5; RenderResearchInsights replaced in Phase 2/3/4
    public static string RenderResearchInsights(Lead lead)
    {
        var sb = new StringBuilder();
        var overallScore = lead.Score?.OverallScore ?? 50;

        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"overallScore: {overallScore}");
        sb.AppendLine($"motivationCategory: unknown");
        sb.AppendLine("---");
        sb.AppendLine();

        // Heading
        sb.AppendLine($"# Research & Insights — {lead.FullName}");
        sb.AppendLine();

        // Minimal doc — enrichment removed in Phase 1.5
        sb.AppendLine("*Enrichment pending — check back after the lead has been processed.*");
        return sb.ToString();
    }

    public static string RenderHomeSearchResults(Lead lead, List<Listing> listings)
    {
        var sb = new StringBuilder();
        var b  = lead.BuyerDetails;

        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"searchDate: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"listingCount: {listings.Count}");
        sb.AppendLine("---");
        sb.AppendLine();

        // Heading
        sb.AppendLine($"# Home Search Results — {lead.FullName}");
        sb.AppendLine();

        // Search criteria
        if (b is not null)
        {
            var budget   = b.MaxBudget  is not null ? $" • Up to {FormatCurrency(b.MaxBudget.Value)}" : "";
            var beds     = b.Bedrooms   is not null ? $" • {b.Bedrooms}+ bed" : "";
            var baths    = b.Bathrooms  is not null ? $" • {b.Bathrooms}+ bath" : "";
            sb.AppendLine($"**Search Criteria:** {b.City}, {b.State}{budget}{beds}{baths}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        if (listings.Count == 0)
        {
            sb.AppendLine("*No listings found matching your criteria.*");
            return sb.ToString();
        }

        for (var i = 0; i < listings.Count; i++)
        {
            var l    = listings[i];
            var sqft = l.Sqft is not null ? $" / {l.Sqft.Value:N0} sqft" : "";
            sb.AppendLine($"## {i + 1}. {l.Address}");
            sb.AppendLine($"**{l.City}, {l.State} {l.Zip}** • ${l.Price:N0} • {l.Beds} bed / {l.Baths} bath{sqft}");
            if (l.WhyThisFits is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"> 💡 *{l.WhyThisFits}*");
            }
            if (l.ListingUrl is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"[View Listing]({l.ListingUrl})");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ─── Formatting helpers ──────────────────────────────────────────────────────

    internal static string FormatPhone(string phone)
    {
        // Already formatted: (NNN) NNN-NNNN
        if (AlreadyFormattedPhone().IsMatch(phone))
            return phone;

        // Strip all non-digits
        var digits = NonDigit().Replace(phone, "");

        if (digits.Length == 10)
            return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";

        return phone; // pass through when not 10 digits
    }

    internal static string FormatCurrency(decimal amount)
        => $"${amount:N0}";

    internal static string FormatTimeline(string timeline) => timeline.ToLowerInvariant().Trim() switch
    {
        "asap"       => "ASAP",
        "1-3months"  => "1–3 months",
        "3-6months"  => "3–6 months",
        "6-12months" => "6–12 months",
        "justlooking" => "Just looking",
        _            => timeline
    };

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private static List<string> BuildTags(Lead lead)
    {
        var tags = new List<string>();
        if (lead.LeadType is LeadType.Buyer or LeadType.Both) tags.Add("buyer");
        if (lead.LeadType is LeadType.Seller or LeadType.Both) tags.Add("seller");
        return tags;
    }

    private static void AppendSection(StringBuilder sb, string heading, string content)
    {
        sb.AppendLine($"## {heading}");
        sb.AppendLine(content);
        sb.AppendLine();
    }

    private static void AppendListSection(StringBuilder sb, string heading, List<string> items)
    {
        sb.AppendLine($"## {heading}");
        foreach (var item in items)
            sb.AppendLine($"- {item}");
        sb.AppendLine();
    }

    internal static string EscapeYaml(string? value) =>
        value?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") ?? "";

    [GeneratedRegex(@"^\(\d{3}\) \d{3}-\d{4}$")]
    private static partial Regex AlreadyFormattedPhone();

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigit();
}
