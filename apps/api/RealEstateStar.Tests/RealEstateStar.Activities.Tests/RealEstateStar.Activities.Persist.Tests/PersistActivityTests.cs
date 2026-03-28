using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Activities.Persist;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Activities.Persist.Tests;

public sealed class PersistActivityTests
{
    private readonly Mock<IDocumentStorageProvider> _storageMock = new();
    private readonly PersistActivity _sut;

    public PersistActivityTests()
    {
        _sut = new PersistActivity(_storageMock.Object, NullLogger<PersistActivity>.Instance);

        // Default: ReadDocumentAsync returns null (no existing file)
        _storageMock
            .Setup(s => s.ReadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    // ── Full pipeline result persisted ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FullResult_WritesAllArtifacts()
    {
        var ctx = BuildFullContext();

        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        // Lead Profile, CMA Summary, HomeSearch Summary, Email Draft, Agent Notification, Retry State
        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), "Lead Profile.md", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.CmaSummaryFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.HomeSearchSummaryFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.AgentNotificationDraftFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.RetryStateFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Partial result (CMA failed) ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CmaFailed_SkipsCmaSummary()
    {
        var ctx = BuildFullContext();
        ctx.CmaResult = new CmaWorkerResult(ctx.Lead.Id.ToString(), false, "timeout", null, null, null, null, null);

        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.CmaSummaryFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoCmaResult_SkipsCmaSummary()
    {
        var ctx = BuildFullContext();
        ctx.CmaResult = null;

        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.CmaSummaryFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoHsResult_SkipsHomeSearchSummary()
    {
        var ctx = BuildFullContext();
        ctx.HsResult = null;

        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.HomeSearchSummaryFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Communication dedup ──────────────────────────────────────────────────

    [Fact]
    public async Task PersistCommunicationAsync_SameHashAndSent_Skips()
    {
        var hash = "abc123";
        var existingFile = $"---\ncontent_hash: {hash}\nsent: true\n---\n\nbody";

        _storageMock
            .Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);

        var record = BuildCommunicationRecord(hash, sent: true);
        await _sut.PersistCommunicationAsync(record, "folder", PersistActivity.LeadEmailDraftFile, CancellationToken.None);

        _storageMock.Verify(
            s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _storageMock.Verify(
            s => s.UpdateDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PersistCommunicationAsync_SameHashNotSent_Overwrites()
    {
        var hash = "abc123";
        var existingFile = $"---\ncontent_hash: {hash}\nsent: false\n---\n\nbody";

        _storageMock
            .Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);

        var record = BuildCommunicationRecord(hash, sent: false);
        await _sut.PersistCommunicationAsync(record, "folder", PersistActivity.LeadEmailDraftFile, CancellationToken.None);

        _storageMock.Verify(
            s => s.UpdateDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PersistCommunicationAsync_DifferentHash_Overwrites()
    {
        var existingFile = "---\ncontent_hash: old_hash\nsent: true\n---\n\nbody";

        _storageMock
            .Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);

        var record = BuildCommunicationRecord("new_hash", sent: true);
        await _sut.PersistCommunicationAsync(record, "folder", PersistActivity.LeadEmailDraftFile, CancellationToken.None);

        _storageMock.Verify(
            s => s.UpdateDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Retry state serialization ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WritesRetryStateAsJson()
    {
        var ctx = BuildMinimalContext();
        ctx.RetryState.CompletedActivityKeys["cma"] = "somehash";
        ctx.RetryState.CompletedResultPaths["cma"] = "path/to/cma";

        string? writtenContent = null;
        _storageMock
            .Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(), PersistActivity.RetryStateFile,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, content, _) => writtenContent = content);

        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        writtenContent.Should().NotBeNull();
        writtenContent.Should().Contain("CompletedActivityKeys");
        writtenContent.Should().Contain("cma");
        writtenContent.Should().Contain("somehash");
    }

    // ── Idempotent ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CalledTwiceWithSameInput_UsesUpdateSecondTime()
    {
        var ctx = BuildFullContext();

        // First call: no existing files → WriteDocumentAsync
        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        // Second call: files exist → UpdateDocumentAsync
        _storageMock
            .Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("existing content");

        // For email dedup simulation: provide a different hash so it does update
        _storageMock
            .Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), PersistActivity.LeadEmailDraftFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("---\ncontent_hash: different_hash\nsent: false\n---");
        _storageMock
            .Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), PersistActivity.AgentNotificationDraftFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("---\ncontent_hash: different_hash\nsent: false\n---");

        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        _storageMock.Verify(
            s => s.UpdateDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    // ── ShouldSkipCommunication unit tests ────────────────────────────────────

    [Fact]
    public void ShouldSkipCommunication_SameHashSent_ReturnsTrue()
    {
        var content = "---\ncontent_hash: abc\nsent: true\n---\nbody";
        PersistActivity.ShouldSkipCommunication(content, "abc").Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipCommunication_SameHashNotSent_ReturnsFalse()
    {
        var content = "---\ncontent_hash: abc\nsent: false\n---\nbody";
        PersistActivity.ShouldSkipCommunication(content, "abc").Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipCommunication_DifferentHashSent_ReturnsFalse()
    {
        var content = "---\ncontent_hash: old\nsent: true\n---\nbody";
        PersistActivity.ShouldSkipCommunication(content, "new").Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipCommunication_NoFrontmatter_ReturnsFalse()
    {
        var content = "Just a plain body without frontmatter";
        PersistActivity.ShouldSkipCommunication(content, "abc").Should().BeFalse();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Lead BuildLead() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = "agent-1",
        LeadType = LeadType.Both,
        FirstName = "Alice",
        LastName = "Tester",
        Email = "alice@example.com",
        Phone = "555-0001",
        Timeline = "asap",
        Status = LeadStatus.Complete,
        ReceivedAt = DateTime.UtcNow,
        SellerDetails = new SellerDetails
        {
            Address = "10 Pine St",
            City = "Newark",
            State = "NJ",
            Zip = "07101"
        },
        BuyerDetails = new BuyerDetails
        {
            City = "Newark",
            State = "NJ"
        }
    };

    private static AgentNotificationConfig BuildAgentConfig() => new()
    {
        AgentId = "agent-1",
        Handle = "agent-1",
        Name = "Test Agent",
        FirstName = "Test",
        Email = "agent@example.com",
        Phone = "555-0002",
        LicenseNumber = "NJ-001",
        BrokerageName = "Test Realty",
        PrimaryColor = "#000",
        AccentColor = "#fff",
        State = "NJ",
        ServiceAreas = []
    };

    private LeadPipelineContext BuildMinimalContext()
    {
        return new LeadPipelineContext
        {
            Lead = BuildLead(),
            AgentConfig = BuildAgentConfig(),
            CorrelationId = "corr-001"
        };
    }

    private LeadPipelineContext BuildFullContext()
    {
        var ctx = BuildMinimalContext();
        var leadId = ctx.Lead.Id.ToString();

        ctx.Score = new LeadScore { OverallScore = 80, Factors = [], Explanation = "Hot lead" };

        ctx.CmaResult = new CmaWorkerResult(
            leadId, true, null,
            500_000m, 480_000m, 520_000m,
            [new CompSummary("123 Main St", 490_000m, 3, 2m, 1800, 14, 0.3, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))],
            "Market is strong.");

        ctx.HsResult = new HomeSearchWorkerResult(
            leadId, true, null,
            [new ListingSummary("456 Oak Ave", 510_000m, 3, 2m, 1900, "Active", null)],
            "Good inventory.");

        ctx.LeadEmail = BuildCommunicationRecord("email-hash-001", sent: true);
        ctx.AgentNotification = BuildCommunicationRecord("notif-hash-001", sent: true);

        return ctx;
    }

    private static CommunicationRecord BuildCommunicationRecord(string contentHash, bool sent) => new()
    {
        Subject = "Test Subject",
        HtmlBody = "<p>Test body</p>",
        Channel = "email",
        DraftedAt = DateTimeOffset.UtcNow,
        Sent = sent,
        ContentHash = contentHash
    };
}
