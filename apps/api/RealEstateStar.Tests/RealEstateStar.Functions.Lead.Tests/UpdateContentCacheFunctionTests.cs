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
/// Tests for <see cref="UpdateContentCacheFunction"/>.
/// </summary>
public class UpdateContentCacheFunctionTests
{
    private readonly FakeDistributedContentCache _cache = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private UpdateContentCacheFunction Build() =>
        new(_cache, NullLogger<UpdateContentCacheFunction>.Instance);

    [Fact]
    public async Task Does_nothing_when_both_results_are_null()
    {
        var input = new UpdateContentCacheInput
        {
            CmaInputHash = "cma_h",
            HsInputHash = "hs_h",
            CmaResult = null,
            HsResult = null,
            CorrelationId = "corr"
        };

        await Build().RunAsync(input, _ct);

        _cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task Does_not_cache_failed_cma_result()
    {
        var failedCma = new CmaWorkerResult("l1", false, "No comps", null, null, null, null, null);

        var input = new UpdateContentCacheInput
        {
            CmaInputHash = "cma_h",
            HsInputHash = "hs_h",
            CmaResult = failedCma,
            HsResult = null,
            CorrelationId = "corr"
        };

        await Build().RunAsync(input, _ct);

        _cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task Does_not_cache_failed_hs_result()
    {
        var failedHs = new HomeSearchWorkerResult("l1", false, "Provider error", null, null);

        var input = new UpdateContentCacheInput
        {
            CmaInputHash = "cma_h",
            HsInputHash = "hs_h",
            CmaResult = null,
            HsResult = failedHs,
            CorrelationId = "corr"
        };

        await Build().RunAsync(input, _ct);

        _cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task Caches_successful_cma_result()
    {
        var cmaResult = new CmaWorkerResult("l1", true, null, 500000m, 480000m, 520000m, [], "narrative");

        var input = new UpdateContentCacheInput
        {
            CmaInputHash = "cma_h",
            HsInputHash = "hs_h",
            CmaResult = cmaResult,
            HsResult = null,
            CorrelationId = "corr"
        };

        await Build().RunAsync(input, _ct);

        _cache.Count.Should().Be(1);
        var cached = await _cache.GetAsync<CmaWorkerResult>("cma_h", _ct);
        cached.Should().NotBeNull();
        cached!.EstimatedValue.Should().Be(500000m);
    }

    [Fact]
    public async Task Caches_successful_hs_result()
    {
        var hsResult = new HomeSearchWorkerResult("l1", true, null,
            [new ListingSummary("123 Oak", 300000m, 3, 2m, 1600, "Active", null)], null);

        var input = new UpdateContentCacheInput
        {
            CmaInputHash = "cma_h",
            HsInputHash = "hs_h",
            CmaResult = null,
            HsResult = hsResult,
            CorrelationId = "corr"
        };

        await Build().RunAsync(input, _ct);

        _cache.Count.Should().Be(1);
        var cached = await _cache.GetAsync<HomeSearchWorkerResult>("hs_h", _ct);
        cached.Should().NotBeNull();
        cached!.Listings.Should().HaveCount(1);
    }

    [Fact]
    public async Task Caches_both_results_when_both_successful()
    {
        var cmaResult = new CmaWorkerResult("l1", true, null, 500000m, null, null, [], null);
        var hsResult = new HomeSearchWorkerResult("l1", true, null, [], null);

        var input = new UpdateContentCacheInput
        {
            CmaInputHash = "cma_h",
            HsInputHash = "hs_h",
            CmaResult = cmaResult,
            HsResult = hsResult,
            CorrelationId = "corr"
        };

        await Build().RunAsync(input, _ct);

        _cache.Count.Should().Be(2);
        (await _cache.GetAsync<CmaWorkerResult>("cma_h", _ct)).Should().NotBeNull();
        (await _cache.GetAsync<HomeSearchWorkerResult>("hs_h", _ct)).Should().NotBeNull();
    }
}
