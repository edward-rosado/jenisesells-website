using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Clients.Azure.Tests;

public sealed class PreviewSessionStoreTests
{
    private readonly Mock<TableClient> _tableClient = new();
    private readonly Mock<ILogger<PreviewSessionStore>> _logger = new();

    private PreviewSessionStore CreateSut() =>
        new(_tableClient.Object, _logger.Object);

    private static PreviewSession MakeSession(string sessionId = "sess-abc", string accountId = "acct-1",
        bool revoked = false, DateTime? revokedAt = null) =>
        new(sessionId, accountId,
            ExpiresAt: DateTime.UtcNow.AddHours(24),
            Revoked: revoked,
            RevokedAt: revokedAt);

    private static PreviewSessionEntity MakeEntity(PreviewSession s) => new()
    {
        PartitionKey = s.SessionId,
        RowKey = "session",
        SessionId = s.SessionId,
        AccountId = s.AccountId,
        ExpiresAt = s.ExpiresAt,
        Revoked = s.Revoked,
        RevokedAt = s.RevokedAt,
        ETag = new ETag("\"test-etag\"")
    };

    private void SetupAddSucceeds()
    {
        _tableClient
            .Setup(t => t.AddEntityAsync(
                It.IsAny<PreviewSessionEntity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response>().Object);
    }

    private void SetupAddConflicts()
    {
        _tableClient
            .Setup(t => t.AddEntityAsync(
                It.IsAny<PreviewSessionEntity>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(409, "Conflict"));
    }

    private void SetupGetExists(PreviewSessionEntity entity)
    {
        var response = Response.FromValue<PreviewSessionEntity?>(entity, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<PreviewSessionEntity>(
                entity.PartitionKey, "session", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupGetNotFound(string sessionId)
    {
        var response = Response.FromValue<PreviewSessionEntity?>(null, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<PreviewSessionEntity>(
                sessionId, "session", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupUpdateSucceeds()
    {
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<PreviewSessionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response>().Object);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_AddsEntity_WhenSessionDoesNotExist()
    {
        SetupAddSucceeds();
        var session = MakeSession();
        var sut = CreateSut();

        await sut.CreateAsync(session, CancellationToken.None);

        _tableClient.Verify(t => t.AddEntityAsync(
            It.Is<PreviewSessionEntity>(e =>
                e.PartitionKey == session.SessionId &&
                e.RowKey == "session" &&
                e.AccountId == session.AccountId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenSessionAlreadyExists()
    {
        SetupAddConflicts();
        var session = MakeSession();
        var sut = CreateSut();

        var act = async () => await sut.CreateAsync(session, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already consumed*");
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsSession_WhenExists()
    {
        var session = MakeSession();
        var entity = MakeEntity(session);
        SetupGetExists(entity);

        var sut = CreateSut();
        var result = await sut.GetAsync(session.SessionId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.SessionId.Should().Be(session.SessionId);
        result.AccountId.Should().Be(session.AccountId);
        result.Revoked.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        SetupGetNotFound("nonexistent");

        var sut = CreateSut();
        var result = await sut.GetAsync("nonexistent", CancellationToken.None);

        result.Should().BeNull();
    }

    // ── RevokeAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_SetsRevoked_WhenSessionExists()
    {
        var session = MakeSession();
        var entity = MakeEntity(session);
        SetupGetExists(entity);
        SetupUpdateSucceeds();

        PreviewSessionEntity? captured = null;
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<PreviewSessionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, ETag, TableUpdateMode, CancellationToken>(
                (e, _, _, _) => captured = (PreviewSessionEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        await sut.RevokeAsync(session.SessionId, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Revoked.Should().BeTrue();
        captured.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RevokeAsync_IsIdempotent_WhenAlreadyRevoked()
    {
        var session = MakeSession(revoked: true, revokedAt: DateTime.UtcNow.AddMinutes(-5));
        var entity = MakeEntity(session);
        SetupGetExists(entity);

        var sut = CreateSut();

        // Should not throw, and should NOT call UpdateEntityAsync
        var act = async () => await sut.RevokeAsync(session.SessionId, CancellationToken.None);
        await act.Should().NotThrowAsync();

        _tableClient.Verify(t => t.UpdateEntityAsync(
            It.IsAny<PreviewSessionEntity>(),
            It.IsAny<ETag>(),
            TableUpdateMode.Replace,
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RevokeAsync_Throws_WhenSessionNotFound()
    {
        SetupGetNotFound("missing-session");

        var sut = CreateSut();
        var act = async () => await sut.RevokeAsync("missing-session", CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── RefreshExpiryAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshExpiryAsync_SlidesExpiry_WhenWithinHardCap()
    {
        var session = MakeSession();
        var entity = MakeEntity(session);
        SetupGetExists(entity);

        PreviewSessionEntity? captured = null;
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<PreviewSessionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, ETag, TableUpdateMode, CancellationToken>(
                (e, _, _, _) => captured = (PreviewSessionEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        var createdAt = DateTime.UtcNow; // freshly created
        var result = await sut.RefreshExpiryAsync(session.SessionId, createdAt, CancellationToken.None);

        // Should be approximately 24h from now
        captured!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RefreshExpiryAsync_ClampsToHardCap_WhenCreatedAtIsOld()
    {
        var session = MakeSession();
        var entity = MakeEntity(session);
        SetupGetExists(entity);

        PreviewSessionEntity? captured = null;
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<PreviewSessionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, ETag, TableUpdateMode, CancellationToken>(
                (e, _, _, _) => captured = (PreviewSessionEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        // Created 29.5 days ago — desired +24h exceeds 30-day hard cap
        var createdAt = DateTime.UtcNow.AddDays(-29.5);
        var hardCapDate = createdAt.AddDays(30);

        var result = await sut.RefreshExpiryAsync(session.SessionId, createdAt, CancellationToken.None);

        // Should be clamped to the hard cap (≤ createdAt + 30d)
        captured!.ExpiresAt.Should().BeCloseTo(hardCapDate, TimeSpan.FromSeconds(5));
        result.ExpiresAt.Should().BeCloseTo(hardCapDate, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RefreshExpiryAsync_Throws_WhenSessionNotFound()
    {
        SetupGetNotFound("missing-session");

        var sut = CreateSut();
        var act = async () =>
            await sut.RefreshExpiryAsync("missing-session", DateTime.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
