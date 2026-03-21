namespace RealEstateStar.Domain.Leads.Models;

public record LeadEnrichment
{
    public required string MotivationCategory { get; init; }
    public required string MotivationAnalysis { get; init; }
    public required string ProfessionalBackground { get; init; }
    public required string FinancialIndicators { get; init; }
    public required string TimelinePressure { get; init; }
    public required List<string> ConversationStarters { get; init; }
    public required List<string> ColdCallOpeners { get; init; }

    public static LeadEnrichment Empty() => new()
    {
        MotivationCategory = "unknown",
        MotivationAnalysis = "unknown",
        ProfessionalBackground = "unknown",
        FinancialIndicators = "unknown",
        TimelinePressure = "unknown",
        ConversationStarters = [],
        ColdCallOpeners = []
    };
}
