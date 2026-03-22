namespace RealEstateStar.Api.Features.Leads;

using RealEstateStar.Api.Features.Leads.Submit;

public static class LeadMappers
{
    public static Lead ToLead(this SubmitLeadRequest request, string agentId) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = agentId,
        LeadType = request.LeadType,
        FirstName = request.FirstName,
        LastName = request.LastName,
        Email = request.Email,
        Phone = request.Phone,
        Timeline = request.Timeline,
        Notes = request.Notes,
        ReceivedAt = DateTime.UtcNow,
        Status = LeadStatus.Received,
        SellerDetails = request.Seller is { } s ? MapSellerDetails(s) : null,
        BuyerDetails = request.Buyer is { } b ? MapBuyerDetails(b) : null
    };

    internal static SellerDetails MapSellerDetails(SellerDetailsRequest s) => new()
    {
        Address = s.Address,
        City = s.City,
        State = s.State,
        Zip = s.Zip,
        Beds = s.Beds,
        Baths = s.Baths,
        Sqft = s.Sqft
    };

    internal static BuyerDetails MapBuyerDetails(BuyerDetailsRequest b)
    {
        var (city, state) = ParseCityState(b.DesiredArea);
        return new BuyerDetails
        {
            City = city,
            State = state,
            MinBudget = b.MinPrice,
            MaxBudget = b.MaxPrice,
            Bedrooms = b.MinBeds,
            Bathrooms = b.MinBaths,
            PreApproved = b.PreApproved,
            PreApprovalAmount = b.PreApprovalAmount
        };
    }

    private static (string City, string State) ParseCityState(string desiredArea)
    {
        var commaIndex = desiredArea.IndexOf(',');
        if (commaIndex < 0)
            return (desiredArea.Trim(), string.Empty);

        var city = desiredArea[..commaIndex].Trim();
        var state = desiredArea[(commaIndex + 1)..].Trim();
        return (city, state);
    }

}
