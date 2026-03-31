using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.Coaching.Tests;

public class CoachingWorkerTests
{
    private readonly Mock<IAnthropicClient> _anthropic = new(MockBehavior.Strict);
    private readonly Mock<IContentSanitizer> _sanitizer = new(MockBehavior.Strict);
    private readonly CoachingWorker _sut;

    public CoachingWorkerTests()
    {
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        _sut = new CoachingWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<CoachingWorker>.Instance);
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    private static EmailMessage MakeEmail(string subject = "Subject", string body = "Body") =>
        new("id", subject, body, "agent@example.com", ["client@example.com"],
            new DateTime(2026, 1, 20, 9, 0, 0), null, []);

    private static EmailCorpus MakeCorpus(int sentCount, int inboxCount) =>
        new(
            Enumerable.Range(0, sentCount).Select(_ => MakeEmail()).ToList(),
            Enumerable.Range(0, inboxCount).Select(_ => MakeEmail("Inbox", "Hello")).ToList(),
            null);

    private static DriveIndex EmptyDrive() =>
        new("fid", [], new Dictionary<string, string>(), []);

    private static AgentDiscovery EmptyDiscovery() =>
        new(null, null, null, [], [], [], null, false, ["English"]);

    private static AnthropicResponse MakeResponse(string content = "# Coaching Report") =>
        new(content, 200, 400, 2000.0);

    // ── Insufficient data guard ───────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_BelowMinSent_ReturnsInsufficient()
    {
        var corpus = MakeCorpus(sentCount: 4, inboxCount: 10);
        // Anthropic should NOT be called
        var result = await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.IsInsufficient.Should().BeTrue();
        result.CoachingReportMarkdown.Should().BeNull();
        _anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeAsync_BelowMinInbox_ReturnsInsufficient()
    {
        var corpus = MakeCorpus(sentCount: 10, inboxCount: 3);
        var result = await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.IsInsufficient.Should().BeTrue();
        result.CoachingReportMarkdown.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_ZeroEmails_ReturnsInsufficient()
    {
        var corpus = MakeCorpus(0, 0);
        var result = await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.IsInsufficient.Should().BeTrue();
    }

    [Fact]
    public static void Insufficient_StaticProperty_HasExpectedValues()
    {
        CoachingResult.Insufficient.IsInsufficient.Should().BeTrue();
        CoachingResult.Insufficient.CoachingReportMarkdown.Should().BeNull();
    }

    // ── Sufficient data — happy path ──────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_SufficientData_CallsClaude()
    {
        var corpus = MakeCorpus(sentCount: 5, inboxCount: 5);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        var result = await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.IsInsufficient.Should().BeFalse();
        result.CoachingReportMarkdown.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ExactlyMinEmails_Proceeds()
    {
        var corpus = MakeCorpus(sentCount: 5, inboxCount: 5);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse("# Coaching Report: Jenise"));

        var result = await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.IsInsufficient.Should().BeFalse();
        result.CoachingReportMarkdown.Should().Be("# Coaching Report: Jenise");
    }

    [Fact]
    public async Task AnalyzeAsync_SanitizesAllExternalContent()
    {
        var corpus = MakeCorpus(sentCount: 6, inboxCount: 6);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        // sent + inbox + drive + reviews = at least 4 sanitize calls
        _sanitizer.Verify(s => s.Sanitize(It.IsAny<string>()), Times.AtLeast(4));
    }

    [Fact]
    public async Task AnalyzeAsync_WrapsDataInUserDataTags()
    {
        var corpus = MakeCorpus(sentCount: 5, inboxCount: 5);
        string? capturedMsg = null;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, msg, _, _, _) => capturedMsg = msg)
            .ReturnsAsync(MakeResponse());

        await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        capturedMsg.Should().Contain("<user-data source=\"sent_emails\"");
        capturedMsg.Should().Contain("<user-data source=\"inbox_emails\"");
        capturedMsg.Should().Contain("<user-data source=\"drive_docs\"");
        capturedMsg.Should().Contain("<user-data source=\"reviews_and_profiles\"");
    }

    [Fact]
    public async Task AnalyzeAsync_PromptContainsAgentName()
    {
        var corpus = MakeCorpus(sentCount: 5, inboxCount: 5);
        string? capturedMsg = null;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, msg, _, _, _) => capturedMsg = msg)
            .ReturnsAsync(MakeResponse());

        await _sut.AnalyzeAsync("Jenise Buckalew", corpus, EmptyDrive(), EmptyDiscovery(), default);

        capturedMsg.Should().Contain("Jenise Buckalew");
    }

    [Fact]
    public async Task AnalyzeAsync_WithReviews_IncludesReviewsInPrompt()
    {
        var corpus = MakeCorpus(sentCount: 5, inboxCount: 5);
        var review = new Review("Great agent!", 5, "Jane D.", "Zillow", new DateTime(2026, 1, 1));
        var profile = new ThirdPartyProfile("Zillow", null, [review], null, null, null, [], [], [], []);
        var discovery = new AgentDiscovery(null, null, null, [], [], [profile], null, false, ["English"]);

        string? capturedMsg = null;
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, msg, _, _, _) => capturedMsg = msg)
            .ReturnsAsync(MakeResponse());

        await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), discovery, default);

        capturedMsg.Should().Contain("Great agent!");
    }

    [Fact]
    public async Task AnalyzeAsync_PassesCancellationToken()
    {
        var corpus = MakeCorpus(sentCount: 5, inboxCount: 5);
        var cts = new CancellationTokenSource();
        CancellationToken capturedCt = default;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, _, _, _, ct) => capturedCt = ct)
            .ReturnsAsync(MakeResponse());

        await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), cts.Token);

        capturedCt.Should().Be(cts.Token);
    }

    [Fact]
    public async Task AnalyzeAsync_PromptContainsCoachingCategories()
    {
        var corpus = MakeCorpus(sentCount: 8, inboxCount: 8);
        string? capturedMsg = null;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, msg, _, _, _) => capturedMsg = msg)
            .ReturnsAsync(MakeResponse());

        await _sut.AnalyzeAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        capturedMsg.Should().Contain("Response Time");
        capturedMsg.Should().Contain("Lead Nurturing");
        capturedMsg.Should().Contain("Call-to-Action");
        capturedMsg.Should().Contain("Real Estate Star");
    }
}
