using FluentAssertions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Workers.Activation.ComplianceAnalysis.Tests;

public class ComplianceAnalysisWorkerTests
{
    private static EmailMessage MakeEmailWithDisclaimer(string subject = "Terms") =>
        new(Id: Guid.NewGuid().ToString(), Subject: subject,
            Body: "Equal Housing Opportunity. Licensed in NJ. This email may be confidential.",
            From: "agent@example.com", To: ["client@example.com"],
            Date: DateTime.UtcNow, SignatureBlock: null, Attachments: []);

    private static EmailMessage MakeEmailWithoutDisclaimer() =>
        new(Id: Guid.NewGuid().ToString(), Subject: "Following up",
            Body: "Just wanted to check in on the showing.",
            From: "agent@example.com", To: ["client@example.com"],
            Date: DateTime.UtcNow, SignatureBlock: null, Attachments: []);

    private static EmailCorpus MakeCorpusWith(params EmailMessage[] sent) =>
        new(SentEmails: sent, InboxEmails: [], Signature: null);

    private static DriveIndex MakeEmptyDriveIndex() => new(
        FolderId: "f1", Files: [],
        Contents: new Dictionary<string, string>(), DiscoveredUrls: []);

    private static AgentDiscovery MakeEmptyDiscovery() =>
        new(HeadshotBytes: null, LogoBytes: null, Phone: null,
            Websites: [], Reviews: [], Profiles: [],
            Ga4MeasurementId: null, WhatsAppEnabled: false, Languages: ["English"]);

    private static AnthropicResponse MakeValidResponse() =>
        new(Content: """
            ## Compliance Analysis
            ### Current Legal Language
            Agent uses Equal Housing Opportunity and license disclosure in emails.
            ### Required Inclusions
            Equal Housing: PRESENT. License number: PRESENT. Brokerage: PRESENT.
            ### Agent-Specific Language to Preserve
            Custom confidentiality notice in email footer.
            ### Wording Differences
            Standard wording, no significant deviations.
            ### Missing Items
            - Cookie consent on website: MEDIUM risk
            """,
            InputTokens: 100, OutputTokens: 200, DurationMs: 1500);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — sanitizer called before Claude
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_WithDisclaimerEmails_CallsSanitizerBeforeClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<ComplianceAnalysisWorker>>();

        var callOrder = new List<string>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Callback<string>(_ => callOrder.Add("sanitize"))
            .Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>((_, _, _, _, _, _) => callOrder.Add("claude"))
            .ReturnsAsync(MakeValidResponse());

        var worker = new ComplianceAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWith(MakeEmailWithDisclaimer());

        await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), MakeEmptyDiscovery(), CancellationToken.None);

        callOrder.Should().Contain("sanitize");
        callOrder.Should().Contain("claude");
        callOrder.IndexOf("sanitize").Should().BeLessThan(callOrder.IndexOf("claude"));
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_ValidResponse_ReturnsMarkdownWithRequiredSections()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<ComplianceAnalysisWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidResponse());

        var worker = new ComplianceAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);

        var result = await worker.AnalyzeAsync(MakeCorpusWith(MakeEmailWithDisclaimer()), MakeEmptyDriveIndex(), MakeEmptyDiscovery(), CancellationToken.None);

        result.Should().Contain("## Compliance Analysis");
        result.Should().Contain("### Required Inclusions");
        result.Should().Contain("### Missing Items");
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — malformed response
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_MalformedResponse_ThrowsInvalidOperationException()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<ComplianceAnalysisWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("Just text.", 50, 20, 500));

        var worker = new ComplianceAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);

        var act = () => worker.AnalyzeAsync(MakeCorpusWith(), MakeEmptyDriveIndex(), MakeEmptyDiscovery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing required section*");
    }

    // ---------------------------------------------------------------------------
    // HasDisclaimerLanguage
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Equal Housing Opportunity statement here", true)]
    [InlineData("Licensed in New Jersey", true)]
    [InlineData("DISCLAIMER: this email is confidential", true)]
    [InlineData("This email is subject to disclosure", true)]
    [InlineData("This email contains confidential information", true)]
    [InlineData("I am a REALTOR licensed in NJ", true)]
    [InlineData("Fair Housing is important", true)]
    [InlineData("Hi, following up on your offer", false)]
    [InlineData("The inspection is scheduled for Tuesday", false)]
    public void HasDisclaimerLanguage_DetectsCorrectly(string body, bool expected)
    {
        var result = ComplianceAnalysisWorker.HasDisclaimerLanguage(body);
        result.Should().Be(expected);
    }

    // ---------------------------------------------------------------------------
    // BuildPrompt
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildPrompt_WithDisclaimerEmails_IncludesEmailSection()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var corpus = MakeCorpusWith(MakeEmailWithDisclaimer());

        var prompt = ComplianceAnalysisWorker.BuildPrompt(corpus, MakeEmptyDriveIndex(), MakeEmptyDiscovery(), sanitizer.Object);

        prompt.Should().Contain("Email Disclaimers");
        prompt.Should().Contain("<user-data>");
    }

    [Fact]
    public void BuildPrompt_NonDisclaimerEmailsExcluded()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var corpus = MakeCorpusWith(MakeEmailWithoutDisclaimer());

        var prompt = ComplianceAnalysisWorker.BuildPrompt(corpus, MakeEmptyDriveIndex(), MakeEmptyDiscovery(), sanitizer.Object);

        prompt.Should().NotContain("Email Disclaimers");
    }

    [Fact]
    public void BuildPrompt_IncludesStandardComplianceReferenceList()
    {
        var sanitizer = new Mock<IContentSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);

        var prompt = ComplianceAnalysisWorker.BuildPrompt(MakeCorpusWith(), MakeEmptyDriveIndex(), MakeEmptyDiscovery(), sanitizer.Object);

        prompt.Should().Contain("Equal Housing Opportunity");
        prompt.Should().Contain("Fair Housing Act");
    }

    // ---------------------------------------------------------------------------
    // ValidateMarkdownOutput
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidateMarkdownOutput_MissingCurrentLegalLanguage_Throws()
    {
        var content = "## Compliance Analysis\n### Required Inclusions\ntext\n### Missing Items\ntext";

        var act = () => ComplianceAnalysisWorker.ValidateMarkdownOutput(content);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Current Legal Language*");
    }

    [Fact]
    public void ValidateMarkdownOutput_AllSectionsPresent_DoesNotThrow()
    {
        var content = """
            ## Compliance Analysis
            ### Current Legal Language
            EHO present.
            ### Required Inclusions
            All present.
            ### Missing Items
            None.
            """;

        var act = () => ComplianceAnalysisWorker.ValidateMarkdownOutput(content);
        act.Should().NotThrow();
    }
}
