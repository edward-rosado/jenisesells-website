
namespace RealEstateStar.Domain.Tests.Leads;

public class LeadTests
{
    [Fact]
    public void FullName_CombinesFirstAndLastName()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(), AgentId = "test", LeadType = LeadType.Buyer,
            FirstName = "Jane", LastName = "Doe",
            Email = "j@e.com", Phone = "555", Timeline = "asap"
        };
        Assert.Equal("Jane Doe", lead.FullName);
    }

    [Fact]
    public void LeadEnrichment_Empty_ReturnsUnknownDefaults()
    {
        var empty = LeadEnrichment.Empty();
        Assert.Equal("unknown", empty.MotivationCategory);
        Assert.Equal("unknown", empty.MotivationAnalysis);
        Assert.Empty(empty.ConversationStarters);
        Assert.Empty(empty.ColdCallOpeners);
    }

    [Fact]
    public void LeadScore_Default_ReturnsFiftyWithReason()
    {
        var score = LeadScore.Default("no data");
        Assert.Equal(50, score.OverallScore);
        Assert.Equal("no data", score.Explanation);
        Assert.Empty(score.Factors);
    }
}
