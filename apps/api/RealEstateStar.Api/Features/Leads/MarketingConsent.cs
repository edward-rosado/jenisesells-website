namespace RealEstateStar.Api.Features.Leads;

public record MarketingConsent
{
    public required Guid LeadId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required bool OptedIn { get; init; }
    public required string ConsentText { get; init; }
    public required List<string> Channels { get; init; }
    public required string IpAddress { get; init; }
    public required string UserAgent { get; init; }
    public required DateTime Timestamp { get; init; }
    public string? Action { get; init; }
    public string? Source { get; init; }
}
