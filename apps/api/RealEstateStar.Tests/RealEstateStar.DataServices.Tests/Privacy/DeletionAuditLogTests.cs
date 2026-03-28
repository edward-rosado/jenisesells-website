using Xunit;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Markdown;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
namespace RealEstateStar.DataServices.Tests.Privacy;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class DeletionAuditLogTests
{
    private readonly Mock<IFileStorageProvider> _storage = new();
    private readonly DeletionAuditLog _sut;

    public DeletionAuditLogTests()
    {
        _sut = new DeletionAuditLog(_storage.Object, NullLogger<DeletionAuditLog>.Instance);
    }

    [Fact]
    public async Task RecordInitiationAsync_AppendsRowWithRequiredFields()
    {
        var leadId = Guid.NewGuid();
        await _sut.RecordInitiationAsync("agent1", leadId, "john@test.com", CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.Is<string>(p => p.Contains("Deletion Audit Log")),
            It.Is<List<string>>(r =>
                r.Count >= 5 &&
                r[1] == "agent1" &&
                r[2] == leadId.ToString() &&
                r[3] != "john@test.com" &&   // email must be hashed, not stored in plain text
                r[3].Length == 12 &&          // 12-char truncated SHA-256 hex
                r[4] == "initiated"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordCompletionAsync_AppendsRowWithCompletedAction()
    {
        var leadId = Guid.NewGuid();
        await _sut.RecordCompletionAsync("agent1", leadId, CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.IsAny<string>(),
            It.Is<List<string>>(r => r[4] == "completed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordCompletionAsync_RedactsEmailField()
    {
        var leadId = Guid.NewGuid();
        await _sut.RecordCompletionAsync("agent1", leadId, CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.IsAny<string>(),
            It.Is<List<string>>(r => r[3] == "[REDACTED]"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordInitiationAsync_TimestampIsIso8601Utc()
    {
        var leadId = Guid.NewGuid();
        List<string>? capturedRows = null;
        _storage.Setup(s => s.AppendRowAsync(
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, List<string>, CancellationToken>((_, rows, _) => capturedRows = rows)
            .Returns(Task.CompletedTask);

        await _sut.RecordInitiationAsync("agent1", leadId, "a@b.com", CancellationToken.None);

        Assert.NotNull(capturedRows);
        var timestamp = capturedRows[0];
        Assert.True(DateTime.TryParse(timestamp, out _), "Timestamp should be parseable as ISO 8601");
        Assert.Contains("Z", timestamp, StringComparison.Ordinal);
        Assert.DoesNotContain("+", timestamp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordInitiationAsync_CallsAppendRowWithCorrectPath()
    {
        await _sut.RecordInitiationAsync("agent1", Guid.NewGuid(), "john@test.com", CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.Is<string>(p => p == "Real Estate Star/agent1/Deletion Audit Log"),
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordCompletionAsync_CallsAppendRowWithCorrectPath()
    {
        await _sut.RecordCompletionAsync("agent1", Guid.NewGuid(), CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.Is<string>(p => p == "Real Estate Star/agent1/Deletion Audit Log"),
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
