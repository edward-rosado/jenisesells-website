using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Markdown;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Markdown;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Services;
using RealEstateStar.Domain.HomeSearch.Markdown;
using RealEstateStar.Domain.WhatsApp.Models;
using RealEstateStar.Domain.WhatsApp;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.Domain.Onboarding;

namespace RealEstateStar.Domain.Tests.Leads;

public class LeadTests
{
    [Fact]
    public void FullName_CombinesFirstAndLastName()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            AgentId = "test",
            LeadType = LeadType.Buyer,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "j@e.com",
            Phone = "555",
            Timeline = "asap"
        };
        Assert.Equal("Jane Doe", lead.FullName);
    }

    // TODO: Pipeline redesign — LeadEnrichment.Empty() removed in Phase 1.5; test removed

    [Fact]
    public void LeadScore_Default_ReturnsFiftyWithReason()
    {
        var score = LeadScore.Default("no data");
        Assert.Equal(50, score.OverallScore);
        Assert.Equal("no data", score.Explanation);
        Assert.Empty(score.Factors);
    }
}
