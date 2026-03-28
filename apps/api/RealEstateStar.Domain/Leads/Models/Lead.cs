namespace RealEstateStar.Domain.Leads.Models;

public class Lead
{
    public required Guid Id { get; init; }
    public required string AgentId { get; init; }
    public required LeadType LeadType { get; set; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Timeline { get; init; }
    public SellerDetails? SellerDetails { get; set; }
    public BuyerDetails? BuyerDetails { get; set; }
    public string? Notes { get; init; }
    public DateTime ReceivedAt { get; init; }
    public LeadStatus Status { get; set; }
    public LeadScore? Score { get; set; }
    public Guid? HomeSearchId { get; set; }
    public string? ConsentToken { get; set; }
    public string? ConsentTokenHash { get; init; }
    public bool? MarketingOptedIn { get; set; }
    public int SubmissionCount { get; set; } = 1;
    public LeadRetryState? RetryState { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    public void MergeType(LeadType newType) => LeadType = newType;
    public void MergeSellerDetails(SellerDetails details) => SellerDetails = details;
    public void MergeBuyerDetails(BuyerDetails details) => BuyerDetails = details;
}
