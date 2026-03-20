using RealEstateStar.Api.Features.Leads;

namespace RealEstateStar.Api.Tests.Features.Leads;

public class LeadTests
{
    [Fact]
    public void FullName_CombinesFirstAndLastName()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(), AgentId = "test", LeadTypes = ["buying"],
            FirstName = "Jane", LastName = "Doe",
            Email = "j@e.com", Phone = "555", Timeline = "asap"
        };
        Assert.Equal("Jane Doe", lead.FullName);
    }
}
