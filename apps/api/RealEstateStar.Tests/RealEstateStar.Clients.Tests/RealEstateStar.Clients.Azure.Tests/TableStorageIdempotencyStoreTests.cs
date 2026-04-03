using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Azure.Tests;

public sealed class TableStorageIdempotencyStoreTests
{
    private readonly Mock<TableClient> _tableClient = new();
    private readonly Mock<ILogger<TableStorageIdempotencyStore>> _logger = new();

    private TableStorageIdempotencyStore CreateSut() =>
        new(_tableClient.Object, _logger.Object);

    // ── ParseKey ──────────────────────────────────────────────────────────────

    [Fact]
    public void ParseKey_ThreeSegments_SplitsPipelineInstanceAndStep()
    {
        var (pk, rk) = TableStorageIdempotencyStore.ParseKey("lead:agent1-lead1:email-send");

        pk.Should().Be("lead:agent1-lead1");
        rk.Should().Be("email-send");
    }

    [Fact]
    public void ParseKey_MoreThanThreeSegments_UsesFirstTwoAsPartitionAndRemainingAsRow()
    {
        var (pk, rk) = TableStorageIdempotencyStore.ParseKey("lead:agent1-lead1:step:sub-step");

        pk.Should().Be("lead:agent1-lead1");
        rk.Should().Be("step:sub-step");
    }

    [Fact]
    public void ParseKey_FewerThanThreeSegments_UsesFullKeyAsPartitionAndDefaultAsRow()
    {
        var (pk, rk) = TableStorageIdempotencyStore.ParseKey("simplekey");

        pk.Should().Be("simplekey");
        rk.Should().Be("default");
    }

    [Fact]
    public void ParseKey_TwoSegments_UsesFullKeyAsPartitionAndDefaultAsRow()
    {
        var (pk, rk) = TableStorageIdempotencyStore.ParseKey("lead:only-two");

        pk.Should().Be("lead:only-two");
        rk.Should().Be("default");
    }

    // ── HasCompletedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task HasCompletedAsync_ReturnsFalse_WhenEntityNotFound()
    {
        var noValue = Response.FromValue<IdempotencyEntity?>(null, new Mock<Response>().Object);

        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<IdempotencyEntity>(
                "lead:agent1-lead1", "email-send", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(noValue);

        var sut = CreateSut();
        var result = await sut.HasCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasCompletedAsync_ReturnsTrue_WhenEntityExists()
    {
        var entity = new IdempotencyEntity
        {
            PartitionKey = "lead:agent1-lead1",
            RowKey = "email-send",
            CompletedAt = DateTimeOffset.UtcNow
        };
        var withValue = Response.FromValue<IdempotencyEntity?>(entity, new Mock<Response>().Object);

        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<IdempotencyEntity>(
                "lead:agent1-lead1", "email-send", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(withValue);

        var sut = CreateSut();
        var result = await sut.HasCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasCompletedAsync_ReturnsFalse_On404RequestFailedException()
    {
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<IdempotencyEntity>(
                It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var sut = CreateSut();
        var result = await sut.HasCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasCompletedAsync_Rethrows_OnNon404Exception()
    {
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<IdempotencyEntity>(
                It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, "Internal Error"));

        var sut = CreateSut();
        var act = async () => await sut.HasCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);

        await act.Should().ThrowAsync<RequestFailedException>();
    }

    // ── MarkCompletedAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkCompletedAsync_UpsertsEntityWithCorrectKeys()
    {
        IdempotencyEntity? capturedEntity = null;

        _tableClient
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<IdempotencyEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, TableUpdateMode, CancellationToken>(
                (e, _, _) => capturedEntity = (IdempotencyEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        await sut.MarkCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.PartitionKey.Should().Be("lead:agent1-lead1");
        capturedEntity.RowKey.Should().Be("email-send");
        capturedEntity.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkCompletedAsync_IsIdempotent_DoesNotThrowOnMultipleCalls()
    {
        _tableClient
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<IdempotencyEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        var act = async () =>
        {
            await sut.MarkCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);
            await sut.MarkCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);
        };

        await act.Should().NotThrowAsync();
        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.IsAny<IdempotencyEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── InMemoryIdempotencyStore (round-trip integration) ─────────────────────

    [Fact]
    public async Task InMemory_HasCompletedAsync_ReturnsFalse_Initially()
    {
        var store = new InMemoryIdempotencyStore();
        var result = await store.HasCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InMemory_HasCompletedAsync_ReturnsTrueAfterMarkCompleted()
    {
        var store = new InMemoryIdempotencyStore();
        await store.MarkCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);
        var result = await store.HasCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InMemory_MarkCompletedAsync_IsIdempotent_DoesNotThrow()
    {
        var store = new InMemoryIdempotencyStore();
        var act = async () =>
        {
            await store.MarkCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);
            await store.MarkCompletedAsync("lead:agent1-lead1:email-send", CancellationToken.None);
        };
        await act.Should().NotThrowAsync();
    }
}
