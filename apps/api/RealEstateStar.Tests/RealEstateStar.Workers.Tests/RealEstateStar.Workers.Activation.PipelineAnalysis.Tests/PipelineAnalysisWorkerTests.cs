using FluentAssertions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Activation.PipelineAnalysis.Tests;

public class PipelineAnalysisWorkerTests
{
    private static EmailMessage MakeEmail(string subject = "Showing request", string body = "I would like to schedule a showing.") =>
        new(Id: Guid.NewGuid().ToString(), Subject: subject, Body: body,
            From: "client@example.com", To: ["agent@example.com"],
            Date: DateTime.UtcNow, SignatureBlock: null);

    private static EmailCorpus MakeCorpusWithInboxEmails(int inboxCount, int sentCount = 5) =>
        new(SentEmails: Enumerable.Range(0, sentCount).Select(_ => MakeEmail("Sent email", "body")).ToList(),
            InboxEmails: Enumerable.Range(0, inboxCount).Select(_ => MakeEmail()).ToList(),
            Signature: null);

    private static DriveIndex MakeEmptyDriveIndex() => new(
        FolderId: "folder-1", Files: [],
        Contents: new Dictionary<string, string>(), DiscoveredUrls: []);

    private static AnthropicResponse MakeValidResponse() =>
        new(Content: """
            ## Sales Pipeline
            ### Active Deals
            Approximately 3 active deals detected.
            ### Deal Velocity
            Average 45 days from first contact.
            ### Client Communication Cadence
            Follows up every 2-3 days during active transactions.
            ### Common Bottlenecks
            Inspection scheduling delays.
            ### Transaction Patterns
            Primarily first-time buyers.
            ### Key Relationships
            Preferred lender mentioned frequently.
            """,
            InputTokens: 100, OutputTokens: 200, DurationMs: 1500);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — low data path (< 5 inbox emails)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_InsufficientInboxEmails_ReturnsLowDataMessage()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: 4);

        var result = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        result.Should().Be("Insufficient email history to map pipeline");
        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public async Task AnalyzeAsync_BelowMinimumInboxEmails_NeverCallsClaude(int inboxCount)
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: inboxCount);

        await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — sanitizer called before Claude
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_SufficientEmails_CallsSanitizerBeforeClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        var callOrder = new List<string>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Callback<string>(_ => callOrder.Add("sanitize"))
            .Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>((_, _, _, _, _, _) => callOrder.Add("claude"))
            .ReturnsAsync(MakeValidResponse());

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: 10);

        await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        callOrder.Should().Contain("sanitize");
        callOrder.Should().Contain("claude");
        callOrder.IndexOf("sanitize").Should().BeLessThan(callOrder.IndexOf("claude"));
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_SufficientEmails_ReturnsMarkdown()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: 10);

        var result = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        result.Should().Contain("## Sales Pipeline");
        result.Should().Contain("### Active Deals");
        result.Should().Contain("### Key Relationships");
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — malformed response
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_MalformedResponse_ThrowsInvalidOperationException()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("Incomplete response.", 50, 20, 500));

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: 10);

        var act = () => worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing required section*");
    }

    // ---------------------------------------------------------------------------
    // MinInboxEmailsRequired constant
    // ---------------------------------------------------------------------------

    [Fact]
    public void MinInboxEmailsRequired_IsExactlyFive()
    {
        PipelineAnalysisWorker.MinInboxEmailsRequired.Should().Be(5);
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_IncludesAnonymizationInstruction()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var corpus = MakeCorpusWithInboxEmails(5);
        var driveIndex = MakeEmptyDriveIndex();

        var prompt = PipelineAnalysisWorker.BuildPrompt(corpus, driveIndex, sanitizer.Object);

        prompt.Should().Contain("Client A");
    }

    [Fact]
    public void BuildPrompt_WrapsEmailBodiesInUserDataTags()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var corpus = MakeCorpusWithInboxEmails(5);
        var driveIndex = MakeEmptyDriveIndex();

        var prompt = PipelineAnalysisWorker.BuildPrompt(corpus, driveIndex, sanitizer.Object);

        prompt.Should().Contain("<user-data>");
        prompt.Should().Contain("</user-data>");
    }

    // ---------------------------------------------------------------------------
    // ValidateMarkdownOutput
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidateMarkdownOutput_MissingDealVelocity_Throws()
    {
        var content = "## Sales Pipeline\n### Active Deals\ntext\n### Key Relationships\ntext";

        var act = () => PipelineAnalysisWorker.ValidateMarkdownOutput(content);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Deal Velocity*");
    }
}
