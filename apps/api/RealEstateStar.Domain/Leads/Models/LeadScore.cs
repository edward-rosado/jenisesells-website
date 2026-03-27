namespace RealEstateStar.Domain.Leads.Models;

public record ScoreFactor
{
    public required string Category { get; init; }
    public required int Score { get; init; }
    public required decimal Weight { get; init; }
    public required string Explanation { get; init; }
}

public record LeadScore
{
    public required int OverallScore { get; init; }
    public required List<ScoreFactor> Factors { get; init; }
    public required string Explanation { get; init; }

    public string Bucket => OverallScore switch
    {
        >= 70 => "Hot",
        >= 40 => "Warm",
        _ => "Cool"
    };

    public static LeadScore Default(string reason) => new()
    {
        OverallScore = 50,
        Factors = [],
        Explanation = reason
    };
}
