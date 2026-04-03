using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace RealEstateStar.Clients.Azure.Tests;

public sealed class TableStorageContentCacheTests
{
    private readonly Mock<TableClient> _tableClient = new();
    private readonly Mock<ILogger<TableStorageContentCache>> _logger = new();

    private TableStorageContentCache CreateSut() =>
        new(_tableClient.Object, _logger.Object);

    private sealed record TestPayload(string Name, int Value);

    private void SetupGetReturnsNull(string key)
    {
        var noValue = Response.FromValue<ContentCacheEntity?>(null, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<ContentCacheEntity>(
                "cache", key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(noValue);
    }

    private void SetupGetReturnsEntity(string key, ContentCacheEntity entity)
    {
        var withValue = Response.FromValue<ContentCacheEntity?>(entity, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<ContentCacheEntity>(
                "cache", key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(withValue);
    }

    private void SetupUpsertSucceeds()
    {
        _tableClient
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<ContentCacheEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response>().Object);
    }

    private void SetupDeleteSucceeds(string key)
    {
        _tableClient
            .Setup(t => t.DeleteEntityAsync(
                "cache", key, ETag.All, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response>().Object);
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        const string key = "sha256-abc123";
        SetupGetReturnsNull(key);

        var sut = CreateSut();
        var result = await sut.GetAsync<TestPayload>(key, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenEntryIsExpired()
    {
        const string key = "sha256-expired";
        var entity = new ContentCacheEntity
        {
            PartitionKey = "cache",
            RowKey = key,
            Value = JsonSerializer.Serialize(new TestPayload("test", 42)),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) // already expired
        };
        SetupGetReturnsEntity(key, entity);

        var sut = CreateSut();
        var result = await sut.GetAsync<TestPayload>(key, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsDeserializedValue_WhenEntryIsValid()
    {
        const string key = "sha256-valid";
        var payload = new TestPayload("Jane Doe", 99);
        var entity = new ContentCacheEntity
        {
            PartitionKey = "cache",
            RowKey = key,
            Value = JsonSerializer.Serialize(payload),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) // not yet expired
        };
        SetupGetReturnsEntity(key, entity);

        var sut = CreateSut();
        var result = await sut.GetAsync<TestPayload>(key, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Jane Doe");
        result.Value.Should().Be(99);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_On404RequestFailedException()
    {
        const string key = "sha256-404";
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<ContentCacheEntity>(
                "cache", key, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var sut = CreateSut();
        var result = await sut.GetAsync<TestPayload>(key, CancellationToken.None);

        result.Should().BeNull();
    }

    // ── SetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_UpsertsEntityWithSerializedValue()
    {
        const string key = "sha256-set";
        var payload = new TestPayload("New Entry", 7);
        ContentCacheEntity? capturedEntity = null;

        _tableClient
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<ContentCacheEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, TableUpdateMode, CancellationToken>(
                (e, _, _) => capturedEntity = (ContentCacheEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        await sut.SetAsync(key, payload, TimeSpan.FromHours(6), CancellationToken.None);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.PartitionKey.Should().Be("cache");
        capturedEntity.RowKey.Should().Be(key);
        capturedEntity.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow + TimeSpan.FromHours(6), TimeSpan.FromSeconds(5));

        var deserialized = JsonSerializer.Deserialize<TestPayload>(capturedEntity.Value);
        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("New Entry");
        deserialized.Value.Should().Be(7);
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingEntry()
    {
        const string key = "sha256-overwrite";
        var first = new TestPayload("First", 1);
        var second = new TestPayload("Second", 2);

        ContentCacheEntity? lastCaptured = null;
        _tableClient
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<ContentCacheEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, TableUpdateMode, CancellationToken>(
                (e, _, _) => lastCaptured = (ContentCacheEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        await sut.SetAsync(key, first, TimeSpan.FromHours(1), CancellationToken.None);
        await sut.SetAsync(key, second, TimeSpan.FromHours(1), CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.IsAny<ContentCacheEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        var deserialized = JsonSerializer.Deserialize<TestPayload>(lastCaptured!.Value);
        deserialized!.Name.Should().Be("Second");
    }

    // ── RemoveAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_DeletesEntity_WhenKeyExists()
    {
        const string key = "sha256-remove";
        SetupDeleteSucceeds(key);

        var sut = CreateSut();
        await sut.RemoveAsync(key, CancellationToken.None);

        _tableClient.Verify(t => t.DeleteEntityAsync(
            "cache", key, ETag.All, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_IsIdempotent_WhenKeyDoesNotExist()
    {
        const string key = "sha256-missing";
        _tableClient
            .Setup(t => t.DeleteEntityAsync(
                "cache", key, ETag.All, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var sut = CreateSut();
        var act = async () => await sut.RemoveAsync(key, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── Serialization round-trip ──────────────────────────────────────────────

    [Fact]
    public async Task Roundtrip_SetThenGet_ReturnsOriginalValue()
    {
        const string key = "sha256-roundtrip";
        var payload = new TestPayload("Round Trip", 1234);

        // Capture what was upserted
        ContentCacheEntity? storedEntity = null;
        _tableClient
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<ContentCacheEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<ITableEntity, TableUpdateMode, CancellationToken>(
                (e, _, _) => storedEntity = (ContentCacheEntity)e)
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        await sut.SetAsync(key, payload, TimeSpan.FromHours(1), CancellationToken.None);

        // Now feed the stored entity back via GetAsync mock
        storedEntity.Should().NotBeNull();
        var withValue = Response.FromValue<ContentCacheEntity?>(storedEntity!, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<ContentCacheEntity>(
                "cache", key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(withValue);

        var result = await sut.GetAsync<TestPayload>(key, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Round Trip");
        result.Value.Should().Be(1234);
    }

    // ── Concurrent access ─────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentGetAsync_BothReturnNull_WhenKeyAbsent()
    {
        const string key = "sha256-concurrent-get";
        var noValue = Response.FromValue<ContentCacheEntity?>(null, new Mock<Response>().Object);
        _tableClient
            .Setup(t => t.GetEntityIfExistsAsync<ContentCacheEntity>(
                "cache", key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(noValue);

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        var task1 = sut.GetAsync<TestPayload>(key, cts.Token);
        var task2 = sut.GetAsync<TestPayload>(key, cts.Token);
        var results = await Task.WhenAll(task1, task2);

        results[0].Should().BeNull();
        results[1].Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentSetAsync_BothCallUpsert()
    {
        const string key = "sha256-concurrent-set";
        var payload1 = new TestPayload("Alpha", 1);
        var payload2 = new TestPayload("Beta", 2);

        _tableClient
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<ContentCacheEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response>().Object);

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        await Task.WhenAll(
            sut.SetAsync(key, payload1, TimeSpan.FromHours(1), cts.Token),
            sut.SetAsync(key, payload2, TimeSpan.FromHours(1), cts.Token));

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.IsAny<ContentCacheEntity>(), TableUpdateMode.Replace, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
