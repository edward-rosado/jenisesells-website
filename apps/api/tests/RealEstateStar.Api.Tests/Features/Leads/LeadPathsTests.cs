using RealEstateStar.Api.Features.Leads;

namespace RealEstateStar.Api.Tests.Features.Leads;

public class LeadPathsTests
{
    [Fact]
    public void LeadFolder_ConstructsCorrectPath()
        => Assert.Equal("Real Estate Star/1 - Leads/Jane Doe", LeadPaths.LeadFolder("Jane Doe"));

    [Fact]
    public void LeadFile_ConstructsCorrectPath()
        => Assert.Equal("Real Estate Star/1 - Leads/Jane Doe/Lead Profile.md", LeadPaths.LeadFile("Jane Doe"));

    [Fact]
    public void EnrichmentFile_ConstructsCorrectPath()
        => Assert.Equal("Real Estate Star/1 - Leads/Jane Doe/Research & Insights.md", LeadPaths.EnrichmentFile("Jane Doe"));

    [Fact]
    public void HomeSearchFile_IncludesDate()
    {
        var date = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(
            "Real Estate Star/1 - Leads/Jane Doe/Home Search/2026-03-19-Home Search Results.md",
            LeadPaths.HomeSearchFile("Jane Doe", date));
    }

    [Fact]
    public void CmaFolder_IncludesAddress()
        => Assert.Equal(
            "Real Estate Star/1 - Leads/Jane Doe/123 Main St",
            LeadPaths.CmaFolder("Jane Doe", "123 Main St"));

    [Fact]
    public void DeletionAuditLogSheet_IncludesAgentId()
        => Assert.Equal(
            "Real Estate Star/test-agent/Deletion Audit Log",
            LeadPaths.DeletionAuditLogSheet("test-agent"));

    [Fact]
    public void Constants_MatchExpectedValues()
    {
        Assert.Equal("Real Estate Star", LeadPaths.Root);
        Assert.Equal("Real Estate Star/1 - Leads", LeadPaths.LeadsFolder);
        Assert.Equal("Real Estate Star/Marketing Consent Log", LeadPaths.ConsentLogSheet);
        Assert.Equal("Real Estate Star/Deletion Audit Log", LeadPaths.DeletionLogSheet);
    }
}
