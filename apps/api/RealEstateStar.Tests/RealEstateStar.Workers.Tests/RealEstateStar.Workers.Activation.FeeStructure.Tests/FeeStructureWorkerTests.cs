using FluentAssertions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Activation.FeeStructure.Tests;

public class FeeStructureWorkerTests
{
    private static EmailMessage MakeFeeEmail(string subject = "Commission discussion", string body = "The commission is 2.5% on buyer side.") =>
        new(Id: Guid.NewGuid().ToString(), Subject: subject, Body: body,
            From: "agent@example.com", To: ["client@example.com"],
            Date: DateTime.UtcNow, SignatureBlock: null);

    private static EmailMessage MakeRegularEmail() =>
        new(Id: Guid.NewGuid().ToString(), Subject: "Showing confirmed",
            Body: "I confirmed the showing for Tuesday at 3pm.",
            From: "agent@example.com", To: ["client@example.com"],
            Date: DateTime.UtcNow, SignatureBlock: null);

    private static DriveFile MakeFeeDoc(string name = "Listing Agreement.pdf") =>
        new(Id: "doc-1", Name: name, MimeType: "application/pdf", Category: "contract", ModifiedDate: DateTime.UtcNow);

    private static EmailCorpus MakeCorpusWith(params EmailMessage[] emails) =>
        new(SentEmails: emails, InboxEmails: [], Signature: null);

    private static DriveIndex MakeEmptyDriveIndex() => new(
        FolderId: "f1", Files: [],
        Contents: new Dictionary<string, string>(), DiscoveredUrls: []);

    private static AnthropicResponse MakeValidResponse() =>
        new(Content: """
            ## Commission Structure
            - Seller-side rate: 2.5%
            - Buyer-side rate: 2.5%
            - Total typical commission: 5%
            ### Brokerage Split
            70/30 agent/brokerage split.
            ### Fee Model
            Percentage-based, negotiable.
            ### Referral Fees
            25% referral fee for agent-to-agent referrals.
            ### Negotiation Patterns
            Commission discussed early in listing presentations.
            ### Other Fees
            - Earnest money typical: 1% of purchase price
            - Closing cost allocation: Seller pays transfer tax, buyer pays mortgage fees.
            """,
            InputTokens: 100, OutputTokens: 200, DurationMs: 1500);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — skip when no fee data
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_NoFeeData_ReturnsNull()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<FeeStructureWorker>>();

        var worker = new FeeStructureWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeRegularEmail());

        var result = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), [], CancellationToken.None);

        result.Should().BeNull();
        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — sanitizer called before Claude
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_WithFeeEmails_CallsSanitizerBeforeClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<FeeStructureWorker>>();

        var callOrder = new List<string>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Callback<string>(_ => callOrder.Add("sanitize"))
            .Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>((_, _, _, _, _, _) => callOrder.Add("claude"))
            .ReturnsAsync(MakeValidResponse());

        var worker = new FeeStructureWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeFeeEmail());

        await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), [], CancellationToken.None);

        callOrder.Should().Contain("sanitize");
        callOrder.Should().Contain("claude");
        callOrder.IndexOf("sanitize").Should().BeLessThan(callOrder.IndexOf("claude"));
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_WithFeeEmails_ReturnsMarkdown()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<FeeStructureWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new FeeStructureWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeFeeEmail());

        var result = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), [], CancellationToken.None);

        result.Should().Contain("## Commission Structure");
        result.Should().Contain("### Brokerage Split");
        result.Should().Contain("### Negotiation Patterns");
    }

    [Fact]
    public async Task AnalyzeAsync_WithFeeDocs_CallsClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<FeeStructureWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new FeeStructureWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var driveIndex = new DriveIndex("f1", [MakeFeeDoc()], new Dictionary<string, string>(), []);
        var emptyCorpus = MakeCorpusWith();

        await worker.AnalyzeAsync(emptyCorpus, driveIndex, [], CancellationToken.None);

        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), "activation-fee-structure", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — malformed response
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_MalformedResponse_ThrowsInvalidOperationException()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<FeeStructureWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("Incomplete.", 50, 20, 500));

        var worker = new FeeStructureWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeFeeEmail());

        var act = () => worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), [], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing required section*");
    }

    // ---------------------------------------------------------------------------
    // IsFeeRelated
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Commission rate for the listing", "standard body", true)]
    [InlineData("Agreement details", "The commission split is 70/30.", true)]
    [InlineData("Offer submitted", "earnest money deposit required", true)]
    [InlineData("Listing agreement ready", "please review", true)]
    [InlineData("Buyer agency agreement", "body text", true)]
    [InlineData("Closing cost estimate", "breakdown below", true)]
    [InlineData("Showing feedback", "the buyers liked it", false)]
    [InlineData("Open house this weekend", "come visit", false)]
    public void IsFeeRelated_FiltersCorrectly(string subject, string body, bool expected)
    {
        var email = new EmailMessage("1", subject, body, "a@b.com", ["c@d.com"], DateTime.UtcNow, null);
        var result = FeeStructureWorker.IsFeeRelated(email);
        result.Should().Be(expected);
    }

    // ---------------------------------------------------------------------------
    // FilterFeeRelatedDocs
    // ---------------------------------------------------------------------------

    [Fact]
    public void FilterFeeRelatedDocs_CommissionDocIncluded()
    {
        var files = new List<DriveFile>
        {
            new("f1", "Commission Schedule.pdf", "application/pdf", "docs", DateTime.UtcNow),
            new("f2", "Property Photos.zip", "application/zip", "media", DateTime.UtcNow)
        };

        var result = FeeStructureWorker.FilterFeeRelatedDocs(files);

        result.Should().ContainSingle(f => f.Id == "f1");
        result.Should().NotContain(f => f.Id == "f2");
    }

    // ---------------------------------------------------------------------------
    // HasFeeContent
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("https://agent.com/fees", true)]
    [InlineData("https://agent.com/commission-info", true)]
    [InlineData("https://agent.com/pricing", true)]
    [InlineData("https://agent.com/about", false)]
    [InlineData("https://agent.com/listings", false)]
    public void HasFeeContent_FiltersUrlsCorrectly(string url, bool expected)
    {
        var result = FeeStructureWorker.HasFeeContent(url);
        result.Should().Be(expected);
    }

    // ---------------------------------------------------------------------------
    // ValidateMarkdownOutput
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidateMarkdownOutput_MissingBrokerageSplit_Throws()
    {
        var content = "## Commission Structure\ntext\n### Negotiation Patterns\ntext";

        var act = () => FeeStructureWorker.ValidateMarkdownOutput(content);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Brokerage Split*");
    }
}
