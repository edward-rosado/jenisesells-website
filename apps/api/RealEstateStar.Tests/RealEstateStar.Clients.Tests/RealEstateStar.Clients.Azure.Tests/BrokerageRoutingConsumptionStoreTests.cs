using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Clients.Azure.Tests;

public sealed class BrokerageRoutingConsumptionStoreTests
{
    private readonly Mock<TableClient> _tableClient = new();
    private readonly Mock<ILogger<BrokerageRoutingConsumptionStore>> _logger = new();

    private BrokerageRoutingConsumptionStore CreateSut() =>
        new(_tableClient.Object, _logger.Object);

    private static BrokerageRoutingConsumption BuildConsumption(
        string accountId = "acct-1",
        string hash = "abc123",
        int counter = 2,
        bool overrideConsumed = false,
        string? etag = "\"some-etag\"") =>
        new()
        {
            AccountId = accountId,
            PolicyContentHash = hash,
            Counter = counter,
            OverrideConsumed = overrideConsumed,
            LastDecisionAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            ETag = etag
        };

    private void SetupGetReturnsNull(string accountId)
    {
        var noValue = Response.FromValue<RoutingConsumptionEntity?>(null, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<RoutingConsumptionEntity>(
                accountId, "consumption", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(noValue);
    }

    private void SetupGetReturnsEntity(string accountId, RoutingConsumptionEntity entity)
    {
        var withValue = Response.FromValue<RoutingConsumptionEntity?>(entity, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<RoutingConsumptionEntity>(
                accountId, "consumption", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(withValue);
    }

    private void SetupUpdateSucceeds()
    {
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<RoutingConsumptionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response>().Object);
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenAccountDoesNotExist()
    {
        SetupGetReturnsNull("acct-missing");

        var sut = CreateSut();
        var result = await sut.GetAsync("acct-missing", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsPopulatedConsumption_WhenEntityExists()
    {
        var entity = new RoutingConsumptionEntity
        {
            PartitionKey = "acct-1",
            RowKey = "consumption",
            PolicyContentHash = "abc123",
            Counter = 5,
            OverrideConsumed = true,
            LastDecisionAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            ETag = new ETag("\"v1\"")
        };
        SetupGetReturnsEntity("acct-1", entity);

        var sut = CreateSut();
        var result = await sut.GetAsync("acct-1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.AccountId.Should().Be("acct-1");
        result.PolicyContentHash.Should().Be("abc123");
        result.Counter.Should().Be(5);
        result.OverrideConsumed.Should().BeTrue();
        result.LastDecisionAt.Should().Be(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        result.ETag.Should().Be("\"v1\"");
    }

    [Fact]
    public async Task GetAsync_Rethrows_OnUnexpectedException()
    {
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<RoutingConsumptionEntity>(
                It.IsAny<string>(), "consumption", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "Internal Error"));

        var sut = CreateSut();
        var act = async () => await sut.GetAsync("acct-1", CancellationToken.None);

        await act.Should().ThrowAsync<RequestFailedException>();
    }

    // ── SaveIfUnchangedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SaveIfUnchangedAsync_ReturnsTrue_OnSuccess()
    {
        SetupUpdateSucceeds();

        var sut = CreateSut();
        var consumption = BuildConsumption(etag: "\"matching-etag\"");
        var result = await sut.SaveIfUnchangedAsync(consumption, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SaveIfUnchangedAsync_ReturnsFalse_OnETagMismatch()
    {
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<RoutingConsumptionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(412, "Precondition Failed"));

        var sut = CreateSut();
        var consumption = BuildConsumption(etag: "\"stale-etag\"");
        var result = await sut.SaveIfUnchangedAsync(consumption, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SaveIfUnchangedAsync_Rethrows_OnNon412Exception()
    {
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<RoutingConsumptionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "Internal Error"));

        var sut = CreateSut();
        var consumption = BuildConsumption();
        var act = async () => await sut.SaveIfUnchangedAsync(consumption, CancellationToken.None);

        await act.Should().ThrowAsync<RequestFailedException>();
    }

    [Fact]
    public async Task SaveIfUnchangedAsync_UsesETagFromConsumption()
    {
        ETag capturedETag = default;
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<RoutingConsumptionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, ETag, TableUpdateMode, CancellationToken>(
                (_, etag, _, _) => capturedETag = etag)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        var consumption = BuildConsumption(etag: "\"my-specific-etag\"");
        await sut.SaveIfUnchangedAsync(consumption, CancellationToken.None);

        capturedETag.ToString().Should().Be("\"my-specific-etag\"");
    }

    // ── Entity mapping roundtrip ──────────────────────────────────────────────

    [Fact]
    public async Task Roundtrip_GetThenSave_PreservesAllFields()
    {
        var originalEntity = new RoutingConsumptionEntity
        {
            PartitionKey = "acct-roundtrip",
            RowKey = "consumption",
            PolicyContentHash = "sha256-deadbeef",
            Counter = 7,
            OverrideConsumed = true,
            LastDecisionAt = new DateTime(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc),
            ETag = new ETag("\"roundtrip-etag\"")
        };
        SetupGetReturnsEntity("acct-roundtrip", originalEntity);

        RoutingConsumptionEntity? savedEntity = null;
        _tableClient
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<RoutingConsumptionEntity>(),
                It.IsAny<ETag>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, ETag, TableUpdateMode, CancellationToken>(
                (e, _, _, _) => savedEntity = (RoutingConsumptionEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();

        // Read
        var consumption = await sut.GetAsync("acct-roundtrip", CancellationToken.None);
        consumption.Should().NotBeNull();

        // Save back — all fields should be preserved
        await sut.SaveIfUnchangedAsync(consumption!, CancellationToken.None);

        savedEntity.Should().NotBeNull();
        savedEntity!.PartitionKey.Should().Be("acct-roundtrip");
        savedEntity.RowKey.Should().Be("consumption");
        savedEntity.PolicyContentHash.Should().Be("sha256-deadbeef");
        savedEntity.Counter.Should().Be(7);
        savedEntity.OverrideConsumed.Should().BeTrue();
        savedEntity.LastDecisionAt.Should().Be(new DateTime(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc));
        savedEntity.ETag.ToString().Should().Be("\"roundtrip-etag\"");
    }
}
