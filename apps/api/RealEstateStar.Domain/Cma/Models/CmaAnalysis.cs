namespace RealEstateStar.Domain.Cma.Models;

public class CmaAnalysis
{
    public required decimal ValueLow { get; init; }
    public required decimal ValueMid { get; init; }
    public required decimal ValueHigh { get; init; }
    public required string MarketNarrative { get; init; }
    public string? PricingRecommendation { get; init; }
    public string? LeadInsights { get; init; }
    public List<string> ConversationStarters { get; init; } = [];
    public required string MarketTrend { get; init; }
    public required int MedianDaysOnMarket { get; init; }
}
