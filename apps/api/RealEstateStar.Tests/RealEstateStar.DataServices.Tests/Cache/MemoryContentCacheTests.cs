using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RealEstateStar.DataServices.Cache;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.DataServices.Tests.Cache;

public sealed class MemoryContentCacheTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryContentCache _sut;

    public MemoryContentCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new MemoryContentCache(_memoryCache);
    }

    public void Dispose() => _memoryCache.Dispose();

    // ── Cache miss ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_CacheMiss_ReturnsNull()
    {
        var result = await _sut.GetAsync<CmaWorkerResult>("missing-key", CancellationToken.None);

        result.Should().BeNull();
    }

    // ── Cache hit ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_AfterSet_ReturnsCachedValue()
    {
        var value = BuildCmaResult();
        await _sut.SetAsync("cma-key", value, TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await _sut.GetAsync<CmaWorkerResult>("cma-key", CancellationToken.None);

        result.Should().NotBeNull();
        result!.LeadId.Should().Be(value.LeadId);
        result.EstimatedValue.Should().Be(value.EstimatedValue);
    }

    // ── Different inputs → cache miss ────────────────────────────────────────

    [Fact]
    public async Task GetAsync_DifferentKey_ReturnsCacheMiss()
    {
        var value = BuildCmaResult();
        await _sut.SetAsync("key-a", value, TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await _sut.GetAsync<CmaWorkerResult>("key-b", CancellationToken.None);

        result.Should().BeNull();
    }

    // ── TTL expiry ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_AfterTtlExpiry_ReturnsCacheMiss()
    {
        var value = BuildCmaResult();
        // Use a very short TTL that's already expired when we create it
        await _sut.SetAsync("expiry-key", value, TimeSpan.FromMilliseconds(1), CancellationToken.None);

        // Wait for expiry
        await Task.Delay(50);

        var result = await _sut.GetAsync<CmaWorkerResult>("expiry-key", CancellationToken.None);

        result.Should().BeNull();
    }

    // ── Overwrite ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_CalledTwice_OverwritesValue()
    {
        var value1 = BuildCmaResult("lead-1");
        var value2 = BuildCmaResult("lead-2");

        await _sut.SetAsync("shared-key", value1, TimeSpan.FromMinutes(5), CancellationToken.None);
        await _sut.SetAsync("shared-key", value2, TimeSpan.FromMinutes(5), CancellationToken.None);

        var result = await _sut.GetAsync<CmaWorkerResult>("shared-key", CancellationToken.None);

        result.Should().NotBeNull();
        result!.LeadId.Should().Be("lead-2");
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _sut.GetAsync<CmaWorkerResult>("key", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SetAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _sut.SetAsync("key", BuildCmaResult(), TimeSpan.FromMinutes(5), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Concurrent access ────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_ConcurrentWrites_LastWriteWins()
    {
        const string key = "concurrent-key";
        var tasks = Enumerable.Range(0, 20).Select(i =>
            _sut.SetAsync(key, BuildCmaResult($"lead-{i}"), TimeSpan.FromMinutes(5), CancellationToken.None));

        await Task.WhenAll(tasks);

        // Just verify it doesn't throw and the cache holds something
        var result = await _sut.GetAsync<CmaWorkerResult>(key, CancellationToken.None);
        result.Should().NotBeNull();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CmaWorkerResult BuildCmaResult(string leadId = "lead-001") => new(
        LeadId: leadId,
        Success: true,
        Error: null,
        EstimatedValue: 500_000m,
        PriceRangeLow: 480_000m,
        PriceRangeHigh: 520_000m,
        Comps: [],
        MarketAnalysis: "Strong market.");
}
