using System.Globalization;
using System.Text;

namespace RealEstateStar.DataServices.Leads;

/// <summary>
/// Formats lead brief content for Drive document storage.
/// Extracted from GwsService — these are lead-domain formatting helpers,
/// not GWS client logic.
/// </summary>
public static class LeadBriefFormatter
{
    public static string BuildLeadFolderPath(string leadName, string address) =>
        $"Real Estate Star/1 - Leads/{leadName}/{address}";

    public static string BuildLeadBriefContent(LeadBriefData data)
    {
        var firstName = data.LeadName.Split(' ')[0];
        var sb = new StringBuilder();

        sb.AppendLine($"New Lead Brief - {data.LeadName}");
        sb.AppendLine("========================================");
        sb.AppendLine();
        sb.AppendLine($"Property: {data.Address}");
        sb.AppendLine($"Timeline: {data.Timeline}");
        sb.AppendLine($"Submitted: {data.SubmittedAt:MMMM d, yyyy} at {data.SubmittedAt:h:mm tt}");
        sb.AppendLine();

        sb.AppendLine($"About {firstName}:");
        if (data.Occupation is not null || data.Employer is not null)
            sb.AppendLine($"  {data.Occupation} at {data.Employer}");
        if (data.PurchaseDate.HasValue && data.PurchasePrice.HasValue)
            sb.AppendLine($"  Purchased {data.Address} in {data.PurchaseDate.Value:MMMM yyyy} for {data.PurchasePrice.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        if (data.OwnershipDuration is not null)
            sb.AppendLine($"  Owned for {data.OwnershipDuration}");
        if (data.EquityRange is not null)
            sb.AppendLine($"  Estimated equity: {data.EquityRange}");
        if (data.LifeEvent is not null)
            sb.AppendLine($"  {data.LifeEvent}");
        sb.AppendLine();

        sb.AppendLine("Property Details (public records):");
        if (data.Beds.HasValue && data.Baths.HasValue && data.Sqft.HasValue && data.YearBuilt.HasValue)
            sb.AppendLine($"  {data.Beds} bed / {data.Baths} bath / {data.Sqft.Value:N0} sqft, built {data.YearBuilt}");
        if (data.LotSize is not null)
            sb.AppendLine($"  Lot: {data.LotSize}");
        if (data.TaxAssessment.HasValue)
            sb.AppendLine($"  Current tax assessment: {data.TaxAssessment.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        if (data.AnnualTax.HasValue)
            sb.AppendLine($"  Annual property taxes: {data.AnnualTax.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}");
        sb.AppendLine();

        sb.AppendLine("Market Context:");
        sb.AppendLine($"  {data.CompCount} comparable sales found in {data.SearchRadius}");
        sb.AppendLine($"  Estimated current value: {data.ValueRange}");
        sb.AppendLine($"  Median days on market: {data.MedianDom}");
        sb.AppendLine($"  Market trending: {data.MarketTrend} market");
        sb.AppendLine();

        sb.AppendLine("Conversation Starters:");
        foreach (var starter in data.ConversationStarters)
            sb.AppendLine($"  \"{starter}\"");
        sb.AppendLine();

        sb.AppendLine($"CMA Status: Sent to {data.LeadEmail}");
        sb.AppendLine($"CMA Report: {data.PdfLink}");
        sb.AppendLine();

        sb.AppendLine("Recommended Next Steps:");
        var priorityAction = data.Timeline switch
        {
            "ASAP" => "Call within 1 hour — this lead is ready NOW",
            "1-3 months" => "Call within 2 hours — serious seller, time-sensitive",
            _ => "Call within 24 hours — build the relationship early"
        };
        sb.AppendLine($"  1. {priorityAction}");
        sb.AppendLine("  2. Reference their situation naturally in conversation");
        sb.AppendLine("  3. Schedule walkthrough");
        sb.AppendLine("  4. Prepare listing agreement");
        sb.AppendLine();

        sb.AppendLine("Contact:");
        sb.AppendLine($"  Phone: {data.LeadPhone}");
        sb.AppendLine($"  Email: {data.LeadEmail}");

        return sb.ToString();
    }
}
