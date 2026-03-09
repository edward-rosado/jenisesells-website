using FluentAssertions;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Tests.Models;

public class StatusMessagesTests
{
    [Theory]
    [InlineData(CmaJobStatus.Parsing, "Received your property details")]
    [InlineData(CmaJobStatus.SearchingComps, "Searching MLS databases...")]
    [InlineData(CmaJobStatus.ResearchingLead, "Researching property records...")]
    [InlineData(CmaJobStatus.Analyzing, "Analyzing market trends...")]
    [InlineData(CmaJobStatus.GeneratingPdf, "Generating your personalized report...")]
    [InlineData(CmaJobStatus.OrganizingDrive, "Organizing documents...")]
    [InlineData(CmaJobStatus.SendingEmail, "Sending report to your email...")]
    [InlineData(CmaJobStatus.Logging, "Finalizing...")]
    [InlineData(CmaJobStatus.Complete, "Your report has been sent to your email!")]
    [InlineData(CmaJobStatus.Failed, "An error occurred while processing your report.")]
    public void Get_ReturnsCorrectMessage_ForStatus(CmaJobStatus status, string expected)
    {
        StatusMessages.Get(status).Should().Be(expected);
    }

    [Fact]
    public void Get_ReturnsDefaultMessage_ForUnknownStatus()
    {
        StatusMessages.Get((CmaJobStatus)999).Should().Be("Processing...");
    }
}
