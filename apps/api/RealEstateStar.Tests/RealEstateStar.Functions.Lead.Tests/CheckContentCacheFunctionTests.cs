using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Functions.Lead.Activities;
using RealEstateStar.Functions.Lead.Models;
using RealEstateStar.TestUtilities;
using CmaWorkerResult = RealEstateStar.Domain.Leads.Models.CmaWorkerResult;
using HomeSearchWorkerResult = RealEstateStar.Domain.Leads.Models.HomeSearchWorkerResult;
using ListingSummary = RealEstateStar.Domain.Leads.Models.ListingSummary;

namespace RealEstateStar.Functions.Lead.Tests;

/// <summary>
/// Tests for <see cref="CheckContentCacheFunction"/>.
/// </summary>
public class CheckContentCacheFunctionTests
{
    private readonly FakeDistributedContentCache _cache = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private CheckContentCacheFunction Build() =>
        new(_cache, NullLogger<CheckContentCacheFunction>.Instance);

    private static CheckContentCacheInput BuildInput(string cmaHash = "cma_hash", string hsHash = "hs_hash") =>
        new()
        {
            CmaInputHash = cmaHash,
            HsInputHash = hsHash,
            CorrelationId = "corr-test"
        };

    [Fact]
    public async Task Returns_no_cache_hits_when_cache_is_empty()
    {
        var result = await Build().RunAsync(BuildInput(), _ct);

        result.CmaCacheHit.Should().BeFalse();
        result.HsCacheHit.Should().BeFalse();
        result.CachedCmaResult.Should().BeNull();
        result.CachedHsResult.Should().BeNull();
    }

    [Fact]
    public async Task Returns_cma_cache_hit_when_cma_in_cache()
    {
        var cmaResult = new CmaWorkerResult("l1", true, null, 500000m, 480000m, 520000m, [], "Great market");
        await _cache.SetAsync("cma_hash", cmaResult, TimeSpan.FromHours(1), _ct);

        var result = await Build().RunAsync(BuildInput(), _ct);

        result.CmaCacheHit.Should().BeTrue();
        result.CachedCmaResult.Should().NotBeNull();
        result.CachedCmaResult!.EstimatedValue.Should().Be(500000m);
        result.HsCacheHit.Should().BeFalse();
    }

    [Fact]
    public async Task Returns_hs_cache_hit_when_hs_in_cache()
    {
        var hsResult = new HomeSearchWorkerResult("l1", true, null,
            [new ListingSummary("100 Oak Ave", 380000m, 3, 2m, 1600, "Active", null)], null);
        await _cache.SetAsync("hs_hash", hsResult, TimeSpan.FromHours(1), _ct);

        var result = await Build().RunAsync(BuildInput(), _ct);

        result.HsCacheHit.Should().BeTrue();
        result.CachedHsResult.Should().NotBeNull();
        result.CachedHsResult!.Listings.Should().HaveCount(1);
        result.CmaCacheHit.Should().BeFalse();
    }

    [Fact]
    public async Task Returns_both_cache_hits_when_both_in_cache()
    {
        var cmaResult = new CmaWorkerResult("l1", true, null, 500000m, null, null, [], null);
        var hsResult = new HomeSearchWorkerResult("l1", true, null, [], null);

        await _cache.SetAsync("cma_hash", cmaResult, TimeSpan.FromHours(1), _ct);
        await _cache.SetAsync("hs_hash", hsResult, TimeSpan.FromHours(1), _ct);

        var result = await Build().RunAsync(BuildInput(), _ct);

        result.CmaCacheHit.Should().BeTrue();
        result.HsCacheHit.Should().BeTrue();
    }

    [Fact]
    public async Task Different_hash_does_not_produce_cache_hit()
    {
        var cmaResult = new CmaWorkerResult("l1", true, null, 500000m, null, null, [], null);
        await _cache.SetAsync("different_hash", cmaResult, TimeSpan.FromHours(1), _ct);

        var result = await Build().RunAsync(BuildInput("cma_hash_1", "hs_hash_1"), _ct);

        result.CmaCacheHit.Should().BeFalse();
        result.HsCacheHit.Should().BeFalse();
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Build().RunAsync(BuildInput(), cts.Token));
    }
}
