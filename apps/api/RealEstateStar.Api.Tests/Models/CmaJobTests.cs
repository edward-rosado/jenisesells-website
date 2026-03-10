using FluentAssertions;
using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Tests.Models;

public class CmaJobTests
{
    private static Lead CreateTestLead(string timeline = "ASAP") => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Phone = "555-1234",
        Address = "123 Main St",
        City = "Springfield",
        State = "NJ",
        Zip = "07001",
        Timeline = timeline,
        Beds = 3,
        Baths = 2,
        Sqft = 1800
    };

    [Fact]
    public void NewJob_HasParsingStatus()
    {
        var job = CmaJob.Create("test-agent", CreateTestLead());

        job.Status.Should().Be(CmaJobStatus.Parsing);
        job.Step.Should().Be(0);
        job.Id.Should().NotBeEmpty();
        job.Comps.Should().BeEmpty();
        job.LeadResearch.Should().BeNull();
        job.Analysis.Should().BeNull();
    }

    [Fact]
    public void AdvanceStep_UpdatesStatusAndStep()
    {
        var job = CmaJob.Create("test-agent", CreateTestLead());

        job.AdvanceTo(CmaJobStatus.SearchingComps);

        job.Status.Should().Be(CmaJobStatus.SearchingComps);
        job.Step.Should().Be(1);
    }

    [Fact]
    public void ReportType_IsComprehensive_ForAsap()
    {
        CmaJob.GetReportType("ASAP").Should().Be(ReportType.Comprehensive);
    }

    [Fact]
    public void ReportType_IsLean_ForJustCurious()
    {
        CmaJob.GetReportType("Just curious").Should().Be(ReportType.Lean);
    }

    [Fact]
    public void ReportType_IsStandard_For6To12Months()
    {
        CmaJob.GetReportType("6-12 months").Should().Be(ReportType.Standard);
    }

    [Fact]
    public void Lead_FullName_CombinesFirstAndLast()
    {
        var lead = CreateTestLead();
        lead.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public void Lead_FullAddress_CombinesAllParts()
    {
        var lead = CreateTestLead();
        lead.FullAddress.Should().Be("123 Main St, Springfield, NJ 07001");
    }

    [Fact]
    public void Comp_PricePerSqft_CalculatesCorrectly()
    {
        var comp = new Comp
        {
            Address = "456 Oak Ave",
            SalePrice = 300_000m,
            SaleDate = new DateOnly(2025, 6, 15),
            Beds = 3,
            Baths = 2,
            Sqft = 1500,
            DistanceMiles = 0.5,
            Source = CompSource.Mls
        };

        comp.PricePerSqft.Should().Be(200m);
    }

    [Fact]
    public void Comp_PricePerSqft_ReturnsZero_WhenSqftIsZero()
    {
        var comp = new Comp
        {
            Address = "456 Oak Ave",
            SalePrice = 300_000m,
            SaleDate = new DateOnly(2025, 6, 15),
            Beds = 3,
            Baths = 2,
            Sqft = 0,
            DistanceMiles = 0.5,
            Source = CompSource.Mls
        };

        comp.PricePerSqft.Should().Be(0m);
    }

    [Fact]
    public void CmaJob_TotalSteps_IsNine()
    {
        var job = CmaJob.Create("test-agent", CreateTestLead());
        job.TotalSteps.Should().Be(9);
    }

    [Fact]
    public void ReportType_IsStandard_ForUnknownTimeline()
    {
        CmaJob.GetReportType("unknown").Should().Be(ReportType.Standard);
    }

    [Fact]
    public void ReportType_IsComprehensive_For1To3Months()
    {
        CmaJob.GetReportType("1-3 months").Should().Be(ReportType.Comprehensive);
    }

    [Fact]
    public void ReportType_IsStandard_For3To6Months()
    {
        CmaJob.GetReportType("3-6 months").Should().Be(ReportType.Standard);
    }

    [Fact]
    public void AdvanceTo_ThrowsOnBackwardTransition()
    {
        var job = CmaJob.Create("test-agent", CreateTestLead());
        job.AdvanceTo(CmaJobStatus.Analyzing);

        var act = () => job.AdvanceTo(CmaJobStatus.SearchingComps);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot transition backward from Analyzing to SearchingComps");
    }

    [Fact]
    public void AdvanceTo_ThrowsOnSameStatus()
    {
        var job = CmaJob.Create("test-agent", CreateTestLead());
        job.AdvanceTo(CmaJobStatus.SearchingComps);

        var act = () => job.AdvanceTo(CmaJobStatus.SearchingComps);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot transition backward from SearchingComps to SearchingComps");
    }

    [Fact]
    public void AdvanceTo_ThrowsOnFailedJob()
    {
        var job = CmaJob.Create("test-agent", CreateTestLead());
        job.Fail("Something went wrong");

        var act = () => job.AdvanceTo(CmaJobStatus.Complete);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot advance a failed job");
    }

    [Fact]
    public void AdvanceTo_AllowsForwardTransition()
    {
        var job = CmaJob.Create("test-agent", CreateTestLead());

        job.AdvanceTo(CmaJobStatus.SearchingComps);
        job.AdvanceTo(CmaJobStatus.Analyzing);
        job.AdvanceTo(CmaJobStatus.Complete);

        job.Status.Should().Be(CmaJobStatus.Complete);
        job.CompletedAt.Should().NotBeNull();
    }
}
