namespace RealEstateStar.Api.Features.Leads.Submit;

using System.ComponentModel.DataAnnotations;

public class SubmitLeadRequest
{
    [Required] public required LeadType LeadType { get; init; }
    [Required, StringLength(100)] public required string FirstName { get; init; }
    [Required, StringLength(100)] public required string LastName { get; init; }
    [Required, StringLength(254), EmailAddress] public required string Email { get; init; }
    [Required, StringLength(30), RegularExpression(@"^\+?[\d\s\-().]{7,20}$")] public required string Phone { get; init; }
    [Required, StringLength(200)] public required string Timeline { get; init; }
    public BuyerDetailsRequest? Buyer { get; init; }
    public SellerDetailsRequest? Seller { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
    [Required] public required MarketingConsentRequest MarketingConsent { get; init; }
}

public class BuyerDetailsRequest
{
    [Required, StringLength(200)] public required string DesiredArea { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBeds { get; init; }
    public int? MinBaths { get; init; }
    public string? PreApproved { get; init; }
    public decimal? PreApprovalAmount { get; init; }
}

public class SellerDetailsRequest
{
    [Required, StringLength(300)] public required string Address { get; init; }
    [Required, StringLength(100)] public required string City { get; init; }
    [Required, StringLength(2)] public required string State { get; init; }
    [Required, RegularExpression(@"^\d{5}(-\d{4})?$")] public required string Zip { get; init; }
    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public int? Sqft { get; init; }
}

public class MarketingConsentRequest
{
    [Required] public required bool OptedIn { get; init; }
    [Required] public required string ConsentText { get; init; }
    [Required] public required List<string> Channels { get; init; }
}
