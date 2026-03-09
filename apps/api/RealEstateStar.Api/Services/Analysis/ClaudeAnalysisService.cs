using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Analysis;

public class ClaudeAnalysisService(HttpClient httpClient, string apiKey, ILogger<ClaudeAnalysisService>? logger = null) : IAnalysisService
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-6";
    private const int MaxTokens = 4096;

    public async Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps, LeadResearch? research, ReportType reportType, CancellationToken ct)
    {
        var prompt = BuildPrompt(lead, comps, research, reportType);

        logger?.LogInformation("Sending CMA analysis request to Claude for {Address}", lead.FullAddress);

        var requestBody = JsonSerializer.Serialize(new
        {
            model = Model,
            max_tokens = MaxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        });

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);

        logger?.LogInformation("Received Claude analysis response for {Address}", lead.FullAddress);

        var doc = JsonDocument.Parse(responseJson);
        var content = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("Empty response from Claude API");

        return ParseResponse(content);
    }

    public static string BuildPrompt(Lead lead, List<Comp> comps, LeadResearch? research, ReportType reportType)
    {
        var sb = new StringBuilder();

        // Section 1: Subject Property
        sb.AppendLine("## Subject Property");
        sb.AppendLine($"- Address: {lead.FullAddress}");
        if (lead.Beds.HasValue) sb.AppendLine($"- Beds: {lead.Beds}");
        if (lead.Baths.HasValue) sb.AppendLine($"- Baths: {lead.Baths}");
        if (lead.Sqft.HasValue) sb.AppendLine($"- Sqft: {lead.Sqft:N0}");
        sb.AppendLine($"- Timeline: {lead.Timeline}");
        sb.AppendLine($"- Report Type: {reportType}");
        sb.AppendLine();

        // Section 2: Comparable Sales
        sb.AppendLine("## Comparable Sales");
        foreach (var comp in comps)
        {
            sb.AppendLine($"- {comp.Address} | ${comp.SalePrice:N0} | {comp.SaleDate} | {comp.Beds}bd/{comp.Baths}ba | {comp.Sqft:N0} sqft | ${comp.PricePerSqft:N0}/sqft | {comp.DistanceMiles:F1} mi | {comp.Source}");
            if (comp.DaysOnMarket.HasValue) sb.Append($" | {comp.DaysOnMarket} DOM");
            sb.AppendLine();
        }
        sb.AppendLine();

        // Section 3: Lead Research (only if non-null)
        if (research is not null)
        {
            sb.AppendLine("## Lead Research");
            if (research.Occupation is not null) sb.AppendLine($"- Occupation: {research.Occupation}");
            if (research.Employer is not null) sb.AppendLine($"- Employer: {research.Employer}");
            if (research.PurchaseDate.HasValue) sb.AppendLine($"- Purchase Date: {research.PurchaseDate}");
            if (research.PurchasePrice.HasValue) sb.AppendLine($"- Purchase Price: ${research.PurchasePrice:N0}");
            if (research.TaxAssessment.HasValue) sb.AppendLine($"- Tax Assessment: ${research.TaxAssessment:N0}");
            if (research.EstimatedEquityLow.HasValue && research.EstimatedEquityHigh.HasValue)
                sb.AppendLine($"- Estimated Equity: ${research.EstimatedEquityLow:N0} - ${research.EstimatedEquityHigh:N0}");
            if (research.LifeEventInsight is not null) sb.AppendLine($"- Life Events: {research.LifeEventInsight}");
            if (research.NeighborhoodContext is not null) sb.AppendLine($"- Neighborhood: {research.NeighborhoodContext}");
            sb.AppendLine();
        }

        // Section 4: Instructions
        sb.AppendLine("## Instructions");
        sb.AppendLine("Analyze the subject property and comparable sales data to produce a CMA.");
        sb.AppendLine("Return ONLY valid JSON (no markdown, no code fences) with this exact schema:");
        sb.AppendLine("""
        {
            "valueLow": <number>,
            "valueMid": <number>,
            "valueHigh": <number>,
            "marketNarrative": "<string>",
            "pricingRecommendation": "<string or null>",
            "leadInsights": "<string or null>",
            "conversationStarters": ["<string>", ...],
            "marketTrend": "<string>",
            "medianDaysOnMarket": <number>
        }
        """);

        return sb.ToString();
    }

    public static CmaAnalysis ParseResponse(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var conversationStarters = new List<string>();
        if (root.TryGetProperty("conversationStarters", out var startersElement))
        {
            foreach (var item in startersElement.EnumerateArray())
            {
                var value = item.GetString();
                if (value is not null) conversationStarters.Add(value);
            }
        }

        return new CmaAnalysis
        {
            ValueLow = root.GetProperty("valueLow").GetDecimal(),
            ValueMid = root.GetProperty("valueMid").GetDecimal(),
            ValueHigh = root.GetProperty("valueHigh").GetDecimal(),
            MarketNarrative = root.GetProperty("marketNarrative").GetString()
                ?? throw new JsonException("marketNarrative is required"),
            PricingRecommendation = root.TryGetProperty("pricingRecommendation", out var pr) ? pr.GetString() : null,
            LeadInsights = root.TryGetProperty("leadInsights", out var li) ? li.GetString() : null,
            ConversationStarters = conversationStarters,
            MarketTrend = root.GetProperty("marketTrend").GetString()
                ?? throw new JsonException("marketTrend is required"),
            MedianDaysOnMarket = root.GetProperty("medianDaysOnMarket").GetInt32()
        };
    }
}
