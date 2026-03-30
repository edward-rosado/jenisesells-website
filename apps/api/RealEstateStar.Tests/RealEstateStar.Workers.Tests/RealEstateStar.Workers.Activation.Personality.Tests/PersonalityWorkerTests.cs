using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.Personality.Tests;

public class PersonalityWorkerTests
{
    private readonly Mock<IAnthropicClient> _anthropic = new(MockBehavior.Strict);
    private readonly Mock<IContentSanitizer> _sanitizer = new(MockBehavior.Strict);
    private readonly PersonalityWorker _sut;

    public PersonalityWorkerTests()
    {
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        _sut = new PersonalityWorker(
            _anthropic.Object,
            _sanitizer.Object,
            NullLogger<PersonalityWorker>.Instance);
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    private static EmailMessage MakeEmail(string body = "I work hard for every client.") =>
        new("id1", "Subject", body, "agent@example.com", ["client@example.com"],
            new DateTime(2026, 2, 10), null);

    private static EmailCorpus MakeCorpus(int sentCount) =>
        new(Enumerable.Range(0, sentCount).Select(_ => MakeEmail()).ToList(), [], null);

    private static DriveIndex EmptyDrive() =>
        new("fid", [], new Dictionary<string, string>(), []);

    private static AgentDiscovery EmptyDiscovery() =>
        new(null, null, null, [], [], [], null, false);

    private static AnthropicResponse MakeResponse(string content = "# Personality Profile: Agent") =>
        new(content, 80, 150, 900.0);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_With5Emails_ReturnsLowConfidenceFalse()
    {
        var corpus = MakeCorpus(5);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        var result = await _sut.ExtractAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.IsLowConfidence.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_With4Emails_ReturnsLowConfidenceTrue()
    {
        var corpus = MakeCorpus(4);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        var result = await _sut.ExtractAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.IsLowConfidence.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_SanitizesAllExternalData()
    {
        var corpus = MakeCorpus(5);
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        _sanitizer.Verify(s => s.Sanitize(It.IsAny<string>()), Times.AtLeast(3));
    }

    [Fact]
    public async Task ExtractAsync_WrapsContentInUserDataTags()
    {
        var corpus = MakeCorpus(5);
        string? capturedMsg = null;
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, msg, _, _, _) => capturedMsg = msg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        capturedMsg.Should().Contain("<user-data source=\"sent_emails\"");
        capturedMsg.Should().Contain("<user-data source=\"drive_docs\"");
        capturedMsg.Should().Contain("<user-data source=\"third_party_bios\"");
        capturedMsg.Should().Contain("</user-data>");
    }

    [Fact]
    public async Task ExtractAsync_LowData_IncludesLowConfidenceNoteInPrompt()
    {
        var corpus = MakeCorpus(2);
        string? capturedMsg = null;
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, msg, _, _, _) => capturedMsg = msg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        capturedMsg.Should().Contain("Low confidence");
    }

    [Fact]
    public async Task ExtractAsync_ReturnsClaudeResponseContent()
    {
        var corpus = MakeCorpus(7);
        const string expected = "# Personality Profile: Jenise\n## Core Identity\nWarm and driven.";
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResponse(expected));

        var result = await _sut.ExtractAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), default);

        result.PersonalitySkillMarkdown.Should().Be(expected);
    }

    [Fact]
    public async Task ExtractAsync_PromptContainsAgentName()
    {
        var corpus = MakeCorpus(5);
        string? capturedMsg = null;
        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, msg, _, _, _) => capturedMsg = msg)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise Buckalew", corpus, EmptyDrive(), EmptyDiscovery(), default);

        capturedMsg.Should().Contain("Jenise Buckalew");
    }

    [Fact]
    public async Task ExtractAsync_PassesCancellationToken()
    {
        var corpus = MakeCorpus(5);
        var cts = new CancellationTokenSource();
        CancellationToken capturedCt = default;

        _anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, _, _, _, ct) => capturedCt = ct)
            .ReturnsAsync(MakeResponse());

        await _sut.ExtractAsync("Jenise", corpus, EmptyDrive(), EmptyDiscovery(), cts.Token);

        capturedCt.Should().Be(cts.Token);
    }
}
