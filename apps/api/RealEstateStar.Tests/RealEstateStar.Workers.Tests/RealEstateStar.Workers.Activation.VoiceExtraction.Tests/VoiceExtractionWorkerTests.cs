using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.VoiceExtraction.Tests;

public class VoiceExtractionWorkerTests
{
    private readonly Mock<IAnthropicClient> _anthropic = new(MockBehavior.Strict);
    private readonly Mock<IContentSanitizer> _sanitizer = new(MockBehavior.Strict);
    private readonly VoiceExtractionWorker _sut;

    public VoiceExtractionWorkerTests()
    {
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s); // passthrough by default
        _sut = new VoiceExtractionWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<VoiceExtractionWorker>.Instance);
    }

    // ── Test data helpers ─────────────────────────────────────────────────────

    private static EmailMessage MakeEmail(string subject = "Test subject", string body = "Test body") =>
        new("id1", subject, body, "jenise@example.com", ["client@example.com"],
            new DateTime(2026, 1, 15), null);

    private static EmailCorpus MakeCorpus(int sentCount) =>
        new(
            SentEmails: Enumerable.Range(0, sentCount).Select(_ => MakeEmail()).ToList(),
            InboxEmails: [],
            Signature: null);

    private static DriveIndex EmptyDriveIndex() =>
        new("folder-id", [], new Dictionary<string, string>(), []);

    private static AgentDiscovery EmptyDiscovery() =>
        new(null, null, null, [], [], [], null, false);

    private static AnthropicResponse MakeResponse(string content = "# Voice Profile: Jenise\n## Core Directive\nTest") =>
        new(content, 100, 200, 1500.0);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_WithSufficientEmails_ReturnsLowConfidenceFalse()
    {
        var corpus = MakeCorpus(sentCount: 10);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        var result = await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        result.IsLowConfidence.Should().BeFalse();
        result.VoiceSkillMarkdown.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractAsync_WithFewerThan5Emails_ReturnsLowConfidenceTrue()
    {
        var corpus = MakeCorpus(sentCount: 3);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        var result = await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        result.IsLowConfidence.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_WithZeroEmails_ReturnsLowConfidenceTrue()
    {
        var corpus = MakeCorpus(sentCount: 0);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        var result = await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        result.IsLowConfidence.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_SanitizesEmailContent_BeforeCallingClaude()
    {
        var corpus = MakeCorpus(sentCount: 5);
        string? capturedUserMessage = null;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        // Sanitize must be called at least 3 times (sent_emails, drive_docs, bios)
        _sanitizer.Verify(s => s.Sanitize(It.IsAny<string>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task ExtractAsync_WrapsEmailsInUserDataTags()
    {
        var corpus = MakeCorpus(sentCount: 5);
        string? capturedUserMessage = null;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        capturedUserMessage.Should().Contain("<user-data source=\"sent_emails\"");
        capturedUserMessage.Should().Contain("</user-data>");
        capturedUserMessage.Should().Contain("<user-data source=\"drive_docs\"");
        capturedUserMessage.Should().Contain("<user-data source=\"third_party_bios\"");
    }

    [Fact]
    public async Task ExtractAsync_LowDataFlag_AppearsInPrompt()
    {
        var corpus = MakeCorpus(sentCount: 2);
        string? capturedUserMessage = null;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        capturedUserMessage.Should().Contain("Low confidence");
    }

    [Fact]
    public async Task ExtractAsync_SufficientEmails_DoesNotContainLowDataFlag()
    {
        var corpus = MakeCorpus(sentCount: 10);
        string? capturedUserMessage = null;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        capturedUserMessage.Should().NotContain("NOTE: Fewer than 5 emails");
    }

    [Fact]
    public async Task ExtractAsync_ReturnsClaudeResponseContent()
    {
        var corpus = MakeCorpus(sentCount: 8);
        var expectedContent = "# Voice Profile: Jenise\n## Core Directive\nWarm and professional.";

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse(expectedContent));

        var result = await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), default);

        result.VoiceSkillMarkdown.Should().Be(expectedContent);
    }

    [Fact]
    public async Task ExtractAsync_WithDriveContent_IncludesDocContentInPrompt()
    {
        var corpus = MakeCorpus(sentCount: 5);
        var driveIndex = new DriveIndex(
            "folder-id",
            [],
            new Dictionary<string, string> { ["Marketing Deck.docx"] = "Our properties are top-tier" },
            []);

        string? capturedUserMessage = null;
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, driveIndex, EmptyDiscovery(), default);

        capturedUserMessage.Should().Contain("Marketing Deck.docx");
    }

    [Fact]
    public async Task ExtractAsync_WithThirdPartyBio_IncludesBioInPrompt()
    {
        var corpus = MakeCorpus(sentCount: 5);
        var profile = new ThirdPartyProfile("Zillow", "Jenise is a top NJ agent.", [], null, null, null, [], [], [], []);
        var discovery = new AgentDiscovery(null, null, null, [], [], [profile], null, false);

        string? capturedUserMessage = null;
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), discovery, default);

        capturedUserMessage.Should().Contain("Zillow");
        capturedUserMessage.Should().Contain("Jenise is a top NJ agent.");
    }

    [Fact]
    public async Task ExtractAsync_PassesCancellationToken_ToClaude()
    {
        var corpus = MakeCorpus(sentCount: 5);
        var cts = new CancellationTokenSource();
        CancellationToken capturedCt = default;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, _, _, _, ct) => capturedCt = ct)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDriveIndex(), EmptyDiscovery(), cts.Token);

        capturedCt.Should().Be(cts.Token);
    }
}
