using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;

namespace RealEstateStar.Api.Tests.Features.Leads;

public class LeadMappersTests
{
    private static SubmitLeadRequest MakeRequest(
        string? desiredArea = "Kill Devil Hills, NC",
        bool withBuyer = true,
        bool withSeller = true,
        string? notes = "Test notes") => new()
    {
        LeadTypes = ["buying", "selling"],
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-867-5309",
        Timeline = "3-6 months",
        Notes = notes,
        MarketingConsent = new MarketingConsentRequest
        {
            OptedIn = true,
            ConsentText = "I agree to receive marketing communications.",
            Channels = ["email", "sms"]
        },
        Buyer = withBuyer ? new BuyerDetailsRequest
        {
            DesiredArea = desiredArea ?? "Kill Devil Hills, NC",
            MinPrice = 300_000m,
            MaxPrice = 500_000m,
            MinBeds = 3,
            MinBaths = 2,
            PreApproved = "yes",
            PreApprovalAmount = 480_000m
        } : null,
        Seller = withSeller ? new SellerDetailsRequest
        {
            Address = "123 Main St",
            City = "Kill Devil Hills",
            State = "NC",
            Zip = "27948",
            Beds = 4,
            Baths = 2,
            Sqft = 1800
        } : null
    };

    [Fact]
    public void ToLead_MapsScalarFields()
    {
        var request = MakeRequest();
        var lead = request.ToLead("agent-1");

        Assert.Equal("agent-1", lead.AgentId);
        Assert.Equal(["buying", "selling"], lead.LeadTypes);
        Assert.Equal("Jane", lead.FirstName);
        Assert.Equal("Doe", lead.LastName);
        Assert.Equal("jane@example.com", lead.Email);
        Assert.Equal("555-867-5309", lead.Phone);
        Assert.Equal("3-6 months", lead.Timeline);
        Assert.Equal("Test notes", lead.Notes);
    }

    [Fact]
    public void ToLead_GeneratesNewGuid()
    {
        var lead = MakeRequest().ToLead("agent-1");
        Assert.NotEqual(Guid.Empty, lead.Id);
    }

    [Fact]
    public void ToLead_SetsReceivedAtToApproximatelyUtcNow()
    {
        var before = DateTime.UtcNow;
        var lead = MakeRequest().ToLead("agent-1");
        var after = DateTime.UtcNow;

        Assert.InRange(lead.ReceivedAt, before, after);
    }

    [Fact]
    public void ToLead_SetsStatusToReceived()
    {
        var lead = MakeRequest().ToLead("agent-1");
        Assert.Equal(LeadStatus.Received, lead.Status);
    }

    [Fact]
    public void ToLead_MapsSeller()
    {
        var lead = MakeRequest(withSeller: true).ToLead("agent-1");

        Assert.NotNull(lead.SellerDetails);
        Assert.Equal("123 Main St", lead.SellerDetails.Address);
        Assert.Equal("Kill Devil Hills", lead.SellerDetails.City);
        Assert.Equal("NC", lead.SellerDetails.State);
        Assert.Equal("27948", lead.SellerDetails.Zip);
    }

    [Fact]
    public void ToLead_MapsBuyer_ParsesCityStateFromDesiredArea()
    {
        var lead = MakeRequest(desiredArea: "Kill Devil Hills, NC").ToLead("agent-1");

        Assert.NotNull(lead.BuyerDetails);
        Assert.Equal("Kill Devil Hills", lead.BuyerDetails.City);
        Assert.Equal("NC", lead.BuyerDetails.State);
    }

    [Fact]
    public void ToLead_MapsBuyer_UsesFullStringAsCityWhenNoComma()
    {
        var lead = MakeRequest(desiredArea: "Outer Banks").ToLead("agent-1");

        Assert.NotNull(lead.BuyerDetails);
        Assert.Equal("Outer Banks", lead.BuyerDetails.City);
        Assert.Equal(string.Empty, lead.BuyerDetails.State);
    }

    [Fact]
    public void ToLead_BuyerOnly_NullSeller()
    {
        var lead = MakeRequest(withBuyer: true, withSeller: false).ToLead("agent-1");

        Assert.NotNull(lead.BuyerDetails);
        Assert.Null(lead.SellerDetails);
    }

    [Fact]
    public void ToLead_SellerOnly_NullBuyer()
    {
        var lead = MakeRequest(withBuyer: false, withSeller: true).ToLead("agent-1");

        Assert.Null(lead.BuyerDetails);
        Assert.NotNull(lead.SellerDetails);
    }

    [Fact]
    public void ToLead_MapsNotes_WhenNull()
    {
        var lead = MakeRequest(notes: null).ToLead("agent-1");
        Assert.Null(lead.Notes);
    }

    [Fact]
    public void ToLead_MapsNotes_WhenPresent()
    {
        var lead = MakeRequest(notes: "Call after 5pm").ToLead("agent-1");
        Assert.Equal("Call after 5pm", lead.Notes);
    }
}
