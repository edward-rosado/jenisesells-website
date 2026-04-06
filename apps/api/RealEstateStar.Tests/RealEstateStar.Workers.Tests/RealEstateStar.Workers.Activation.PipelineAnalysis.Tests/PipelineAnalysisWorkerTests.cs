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
        Contents: new Dictionary<string, string>(), DiscoveredUrls: [], Extractions: []);

    private const string ValidPipelineJson = """
        {
          "leads": [
            {
              "id": "L-001",
              "name": "Client A",
              "stage": "showing",
              "type": "buyer",
              "property": "123 Main St, Newark, NJ",
              "source": "referral",
              "firstSeen": "2026-03-15",
              "lastActivity": "2026-04-01",
              "next": "schedule second showing",
              "notes": "interested in 3BR homes"
            },
            {
              "id": "L-002",
              "name": "Client B",
              "stage": "under-contract",
              "type": "seller",
              "property": "456 Oak Ave, Jersey City, NJ",
              "source": "direct",
              "firstSeen": "2026-02-10",
              "lastActivity": "2026-04-02",
              "next": "awaiting inspection results",
              "notes": "downsizing from 4BR"
            }
          ]
        }
        """;

    private static AnthropicResponse MakeValidJsonResponse() =>
        new(Content: ValidPipelineJson, InputTokens: 100, OutputTokens: 200, DurationMs: 1500);

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — low data path (< 5 inbox emails)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_InsufficientInboxEmails_ReturnsNull()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: 4);

        var result = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        result.Should().BeNull();
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
            .ReturnsAsync(MakeValidJsonResponse());

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
    public async Task AnalyzeAsync_SufficientEmails_ReturnsPipelineJsonAndMarkdown()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeValidJsonResponse());

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: 10);

        var result = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.PipelineJson.Should().Contain("\"leads\"");
        result.PipelineJson.Should().Contain("L-001");
        result.Markdown.Should().Contain("## Sales Pipeline");
        result.Markdown.Should().Contain("Client A");
    }

    // ---------------------------------------------------------------------------
    // AnalyzeAsync — invalid JSON response returns null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeAsync_InvalidJsonResponse_ReturnsNull()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<PipelineAnalysisWorker>>();

        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("This is not JSON at all.", 50, 20, 500));

        var worker = new PipelineAnalysisWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpusWithInboxEmails(inboxCount: 10);

        var result = await worker.AnalyzeAsync(corpus, MakeEmptyDriveIndex(), CancellationToken.None);

        result.Should().BeNull();
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
    // ValidatePipelineJson
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidatePipelineJson_ValidJson_ReturnsJson()
    {
        var result = PipelineAnalysisWorker.ValidatePipelineJson(ValidPipelineJson);

        result.Should().NotBeNull();
        result.Should().Contain("\"leads\"");
    }

    [Fact]
    public void ValidatePipelineJson_NullInput_ReturnsNull()
    {
        PipelineAnalysisWorker.ValidatePipelineJson(null).Should().BeNull();
    }

    [Fact]
    public void ValidatePipelineJson_EmptyString_ReturnsNull()
    {
        PipelineAnalysisWorker.ValidatePipelineJson("").Should().BeNull();
    }

    [Fact]
    public void ValidatePipelineJson_WhitespaceOnly_ReturnsNull()
    {
        PipelineAnalysisWorker.ValidatePipelineJson("   ").Should().BeNull();
    }

    [Fact]
    public void ValidatePipelineJson_InvalidJson_ReturnsNull()
    {
        PipelineAnalysisWorker.ValidatePipelineJson("not json at all").Should().BeNull();
    }

    [Fact]
    public void ValidatePipelineJson_MissingLeadsProperty_ReturnsNull()
    {
        PipelineAnalysisWorker.ValidatePipelineJson("""{"deals": []}""").Should().BeNull();
    }

    [Fact]
    public void ValidatePipelineJson_LeadsNotArray_ReturnsNull()
    {
        PipelineAnalysisWorker.ValidatePipelineJson("""{"leads": "not an array"}""").Should().BeNull();
    }

    [Fact]
    public void ValidatePipelineJson_EmptyLeadsArray_ReturnsValidJson()
    {
        var result = PipelineAnalysisWorker.ValidatePipelineJson("""{"leads": []}""");
        result.Should().NotBeNull();
        result.Should().Contain("\"leads\"");
    }

    [Fact]
    public void ValidatePipelineJson_WrappedInCodeFences_StripsAndReturnsJson()
    {
        var wrapped = """
            ```json
            {"leads": [{"id": "L-001", "name": "Client A", "stage": "new", "type": "buyer"}]}
            ```
            """;

        var result = PipelineAnalysisWorker.ValidatePipelineJson(wrapped);

        result.Should().NotBeNull();
        result.Should().Contain("\"leads\"");
        result.Should().NotContain("```");
    }

    // ---------------------------------------------------------------------------
    // GenerateMarkdownSummary
    // ---------------------------------------------------------------------------

    [Fact]
    public void GenerateMarkdownSummary_ValidJson_ContainsPipelineHeader()
    {
        var markdown = PipelineAnalysisWorker.GenerateMarkdownSummary(ValidPipelineJson);

        markdown.Should().Contain("## Sales Pipeline");
    }

    [Fact]
    public void GenerateMarkdownSummary_ValidJson_ContainsTotalLeadCount()
    {
        var markdown = PipelineAnalysisWorker.GenerateMarkdownSummary(ValidPipelineJson);

        markdown.Should().Contain("**Total Leads:** 2");
    }

    [Fact]
    public void GenerateMarkdownSummary_ValidJson_ContainsStageTable()
    {
        var markdown = PipelineAnalysisWorker.GenerateMarkdownSummary(ValidPipelineJson);

        markdown.Should().Contain("### Pipeline by Stage");
        markdown.Should().Contain("| showing | 1 |");
        markdown.Should().Contain("| under-contract | 1 |");
    }

    [Fact]
    public void GenerateMarkdownSummary_ValidJson_ContainsLeadDetailsTable()
    {
        var markdown = PipelineAnalysisWorker.GenerateMarkdownSummary(ValidPipelineJson);

        markdown.Should().Contain("### Lead Details");
        markdown.Should().Contain("| L-001 | Client A | showing | buyer | 123 Main St, Newark, NJ | schedule second showing |");
        markdown.Should().Contain("| L-002 | Client B | under-contract | seller | 456 Oak Ave, Jersey City, NJ | awaiting inspection results |");
    }

    [Fact]
    public void GenerateMarkdownSummary_EmptyLeads_ShowsZeroTotal()
    {
        var markdown = PipelineAnalysisWorker.GenerateMarkdownSummary("""{"leads": []}""");

        markdown.Should().Contain("**Total Leads:** 0");
    }

    [Fact]
    public void GenerateMarkdownSummary_NullFields_ShowsDashes()
    {
        var json = """{"leads": [{"id": "L-001", "name": "Client A", "stage": "new", "type": "buyer", "property": null, "next": null}]}""";

        var markdown = PipelineAnalysisWorker.GenerateMarkdownSummary(json);

        markdown.Should().Contain("| L-001 | Client A | new | buyer | - | - |");
    }

    // ---------------------------------------------------------------------------
    // ValidStages and ValidTypes constants
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidStages_ContainsExpectedValues()
    {
        PipelineAnalysisWorker.ValidStages.Should().BeEquivalentTo(
            ["new", "contacted", "showing", "applied", "under-contract", "closing", "closed", "lost"]);
    }

    [Fact]
    public void ValidTypes_ContainsExpectedValues()
    {
        PipelineAnalysisWorker.ValidTypes.Should().BeEquivalentTo(
            ["sale", "rental", "buyer", "seller"]);
    }
}
