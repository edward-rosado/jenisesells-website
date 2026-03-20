using System.Text;
using System.Text.RegularExpressions;

namespace RealEstateStar.Api.Features.Leads;

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
        sb.AppendLine($"receivedAt: {lead.ReceivedAt:O}");
        sb.AppendLine($"cmaJobId: {lead.CmaJobId?.ToString() ?? ""}");
        sb.AppendLine($"homeSearchId: {lead.HomeSearchId?.ToString() ?? ""}");
        sb.AppendLine();
        sb.AppendLine("# === Indexable ===");
        sb.AppendLine($"firstName: \"{EscapeYaml(lead.FirstName)}\"");
        sb.AppendLine($"lastName: \"{EscapeYaml(lead.LastName)}\"");
        sb.AppendLine($"email: \"{EscapeYaml(lead.Email)}\"");
        sb.AppendLine($"phone: \"{EscapeYaml(lead.Phone)}\"");
        sb.AppendLine($"leadTypes: [{string.Join(", ", lead.LeadTypes)}]");
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

    public static string RenderResearchInsights(Lead lead)
    {
        var sb = new StringBuilder();
        var overallScore        = lead.Score?.OverallScore ?? 50;
        var motivationCategory  = lead.Enrichment?.MotivationCategory ?? "unknown";

        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"overallScore: {overallScore}");
        sb.AppendLine($"motivationCategory: {motivationCategory}");
        sb.AppendLine("---");
        sb.AppendLine();

        // Heading
        sb.AppendLine($"# Research & Insights — {lead.FullName}");
        sb.AppendLine();

        // Minimal doc when enrichment is absent
        if (lead.Enrichment is null)
        {
            sb.AppendLine("*Enrichment pending — check back after the lead has been processed.*");
            return sb.ToString();
        }

        // Score section
        sb.AppendLine($"## Lead Score: {overallScore} / 100");
        sb.AppendLine();
        sb.AppendLine("### Score Breakdown");
        sb.AppendLine("| Category | Score | Weight | Explanation |");
        sb.AppendLine("|----------|-------|--------|-------------|");
        if (lead.Score is not null)
        {
            foreach (var f in lead.Score.Factors)
                sb.AppendLine($"| {f.Category} | {f.Score} | {f.Weight:P0} | {f.Explanation} |");

            sb.AppendLine();
            sb.AppendLine($"**Overall:** {lead.Score.Explanation}");
        }
        sb.AppendLine();

        // Enrichment sections
        AppendSection(sb, "Motivation Analysis",       lead.Enrichment.MotivationAnalysis);
        AppendSection(sb, "Professional Background",   lead.Enrichment.ProfessionalBackground);
        AppendSection(sb, "Financial Indicators",      lead.Enrichment.FinancialIndicators);
        AppendSection(sb, "Timeline Pressure",         lead.Enrichment.TimelinePressure);

        AppendListSection(sb, "Conversation Starters", lead.Enrichment.ConversationStarters);
        AppendListSection(sb, "Cold Call Openers",     lead.Enrichment.ColdCallOpeners);

        sb.AppendLine("---");
        sb.AppendLine($"*Generated by Real Estate Star • {DateTime.UtcNow:MMMM d, yyyy}*");

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
        var tags = new List<string>(lead.LeadTypes);
        if (lead.SellerDetails is not null) tags.Add("seller");
        if (lead.BuyerDetails  is not null) tags.Add("buyer");
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
