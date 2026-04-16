namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Agent discovered during brokerage team page scraping.
/// Stored in Azure Table for future warm-start activations and outbound sales.
/// </summary>
public sealed record ProspectiveAgent
{
    public string AccountId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Title { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? BioSnippet { get; init; }
    public string? HeadshotUrl { get; init; }
    public IReadOnlyList<string> Specialties { get; init; } = [];
    public IReadOnlyList<string> ServiceAreas { get; init; } = [];
    public DateTime ScrapedAt { get; init; }
    public DateTime? LastRefreshedAt { get; init; }
    public string? SourceUrl { get; init; }
}
