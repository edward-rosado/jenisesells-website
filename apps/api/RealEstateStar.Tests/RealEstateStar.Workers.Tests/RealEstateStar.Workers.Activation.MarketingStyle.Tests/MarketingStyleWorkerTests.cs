using FluentAssertions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Activation.MarketingStyle.Tests;

public class MarketingStyleWorkerTests
{
    private static EmailMessage MakeMarketingEmail(string subject = "Just Listed: 123 Main St") =>
        new(Id: Guid.NewGuid().ToString(), Subject: subject,
            Body: "Just listed a beautiful home! View listing at http://example.com",
            From: "agent@example.com", To: ["client@example.com"],
            Date: DateTime.UtcNow, SignatureBlock: null);

    private static EmailMessage MakeRegularEmail() =>
        new(Id: Guid.NewGuid().ToString(), Subject: "Your closing date",
            Body: "Hi, your closing is scheduled for next week.",
            From: "agent@example.com", To: ["client@example.com"],
            Date: DateTime.UtcNow, SignatureBlock: null);

    private static DriveIndex MakeEmptyDriveIndex() => new(
        FolderId: "folder-1", Files: [],
        Contents: new Dictionary<string, string>(), DiscoveredUrls: [], Extractions: []);

    private static EmailCorpus MakeCorpusWith(params EmailMessage[] sent) =>
        new(SentEmails: sent, InboxEmails: [], Signature: null);

    private static AnthropicResponse MakeValidResponse() =>
        new(Content: """
            ## Marketing Style
            ### Campaign Types
            Just listed, open house, market updates.
            ### Email Design Patterns
            Single column with large hero image.
            ### Marketing Voice
            Enthusiastic and upbeat compared to regular communications.
            ### Audience Segmentation
            Buyers and sellers segmented by price tier.
            ### Brand Signals
            Primary color blue (#003366), logo in header.
            """,
            InputTokens: 100, OutputTokens: 200, DurationMs: 1500);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — skip when no marketing emails
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_NoMarketingEmails_ReturnsNullTuple()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<MarketingStyleWorker>>();

        var worker = new MarketingStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeRegularEmail());

        var (style, signals, _) = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        style.Should().BeNull();
        signals.Should().BeNull();
        anthropic.Verify(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMarketingEmails_CallsSanitizerBeforeClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<MarketingStyleWorker>>();

        var callOrder = new List<string>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Callback<string>(_ => callOrder.Add("sanitize"))
            .Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>((_, _, _, _, _, _) => callOrder.Add("claude"))
            .ReturnsAsync(MakeValidResponse());

        var worker = new MarketingStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeMarketingEmail());

        await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        callOrder.Should().StartWith(["sanitize"]);
        callOrder.Should().Contain("claude");
        callOrder.IndexOf("sanitize").Should().BeLessThan(callOrder.IndexOf("claude"));
    }

    [Fact]
    public async Task AnalyzeAsync_ValidResponse_ReturnsBothStyleAndSignals()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<MarketingStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new MarketingStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeMarketingEmail());

        var (style, signals, _) = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        style.Should().NotBeNull();
        style.Should().Contain("## Marketing Style");
        signals.Should().NotBeNull();
        signals.Should().Contain("blue");
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — malformed response
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_MalformedResponse_ThrowsInvalidOperationException()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<MarketingStyleWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("Just plain text.", 50, 20, 500));

        var worker = new MarketingStyleWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeMarketingEmail());

        var act = () => worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing required section*");
    }

    // ---------------------------------------------------------------------------
    // IsMarketingEmail
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Just Listed: 123 Main", "details", true)]
    [InlineData("Open House Sunday!", "come see it", true)]
    [InlineData("Market Update - March", "prices rising", true)]
    [InlineData("New Listing Alert", "check it out", true)]
    [InlineData("Price Reduced on Oak Ave", "details", true)]
    [InlineData("Regular subject", "just listed a property", true)]
    [InlineData("Regular subject", "open house this Saturday", true)]
    [InlineData("Closing checklist", "here is what you need", false)]
    [InlineData("Your offer was accepted", "congratulations", false)]
    public void IsMarketingEmail_FiltersCorrectly(string subject, string body, bool expected)
    {
        var email = new EmailMessage(
            Id: "1", Subject: subject, Body: body,
            From: "a@b.com", To: ["c@d.com"], Date: DateTime.UtcNow, SignatureBlock: null);

        var result = MarketingStyleWorker.IsMarketingEmail(email);
        result.Should().Be(expected);
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_WrapsEmailBodyInUserDataTags()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var emails = new List<EmailMessage> { MakeMarketingEmail() };
        var driveIndex = MakeEmptyDriveIndex();

        var prompt = MarketingStyleWorker.BuildPrompt(emails, driveIndex, sanitizer.Object);

        prompt.Should().Contain("<user-data>");
        prompt.Should().Contain("</user-data>");
    }

    [Fact]
    public void BuildPrompt_CallsSanitizerOnEmailBody()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns("sanitized-body");

        var emails = new List<EmailMessage>
        {
            new("1", "Just Listed", "raw body content", "a@b.com", ["c@d.com"], DateTime.UtcNow, null)
        };

        MarketingStyleWorker.BuildPrompt(emails, MakeEmptyDriveIndex(), sanitizer.Object);

        sanitizer.Verify(s => s.Sanitize("raw body content"), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // ExtractBrandSignals
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExtractBrandSignals_ExtractsBrandSignalsSection()
    {
        var content = """
            ## Marketing Style
            ### Campaign Types
            Just listed, open house.
            ### Email Design Patterns
            Single column.
            ### Marketing Voice
            Enthusiastic.
            ### Audience Segmentation
            Buyers vs sellers.
            ### Brand Signals
            Primary color: blue (#003366).
            Logo: top of email.
            ### SomeOtherSection
            Other content.
            """;

        var result = MarketingStyleWorker.ExtractBrandSignals(content);

        result.Should().Contain("blue");
        result.Should().NotContain("## Marketing Style");
        result.Should().NotContain("### Campaign Types");
    }

    [Fact]
    public void ExtractBrandSignals_NoBrandSignalsSection_ReturnsEmpty()
    {
        var content = """
            ## Marketing Style
            ### Campaign Types
            Just listed.
            """;

        var result = MarketingStyleWorker.ExtractBrandSignals(content);
        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // ValidateMarkdownOutput
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidateMarkdownOutput_MissingCampaignTypes_Throws()
    {
        var content = "## Marketing Style\n### Email Design Patterns\ntext\n### Marketing Voice\nvoice";

        var act = () => MarketingStyleWorker.ValidateMarkdownOutput(content);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Campaign Types*");
    }
}
