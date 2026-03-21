namespace RealEstateStar.Domain.Leads.Models;

public class Lead
{
    public required Guid Id { get; init; }
    public required string AgentId { get; init; }
    public required LeadType LeadType { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Timeline { get; init; }
    public SellerDetails? SellerDetails { get; init; }
    public BuyerDetails? BuyerDetails { get; init; }
    public string? Notes { get; init; }
    public DateTime ReceivedAt { get; init; }
    public LeadStatus Status { get; set; }
    public LeadEnrichment? Enrichment { get; set; }
    public LeadScore? Score { get; set; }
    public Guid? HomeSearchId { get; set; }
    public string? ConsentToken { get; set; }
    public string? ConsentTokenHash { get; init; }
    public bool? MarketingOptedIn { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
