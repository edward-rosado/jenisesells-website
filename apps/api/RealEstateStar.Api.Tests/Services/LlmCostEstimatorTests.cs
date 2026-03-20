using Microsoft.Extensions.Configuration;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Services;

public class LlmCostEstimatorTests
{
    private static LlmCostEstimator CreateEstimator(Dictionary<string, string?>? overrides = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(overrides ?? [])
            .Build();
        return new LlmCostEstimator(config);
    }

    [Fact]
    public void EstimateCost_SonnetDefaults_CalculatesCorrectly()
    {
        var estimator = CreateEstimator();

        // 1000 input @ $3.00/1M + 500 output @ $15.00/1M
        // = 0.003 + 0.0075 = 0.0105
        var cost = estimator.EstimateCost("claude-sonnet-4-6", 1000, 500);

        Assert.Equal(0.0105m, cost);
    }

    [Fact]
    public void EstimateCost_HaikuDefaults_CalculatesCorrectly()
    {
        var estimator = CreateEstimator();

        // 1000 input @ $0.80/1M + 500 output @ $4.00/1M
        // = 0.0008 + 0.002 = 0.0028
        var cost = estimator.EstimateCost("claude-haiku-4-5", 1000, 500);

        Assert.Equal(0.0028m, cost);
    }

    [Fact]
    public void EstimateCost_UnknownModel_FallsBackToSonnetPricing()
    {
        var estimator = CreateEstimator();

        // Should use sonnet defaults: $3.00/1M input, $15.00/1M output
        var cost = estimator.EstimateCost("unknown-model", 1000, 500);

        Assert.Equal(0.0105m, cost);
    }

    [Fact]
    public void EstimateCost_ReadsFromConfig_WhenAvailable()
    {
        var estimator = CreateEstimator(new Dictionary<string, string?>
        {
            ["LlmPricing:claude-sonnet-4-6:InputPer1M"] = "6.00",
            ["LlmPricing:claude-sonnet-4-6:OutputPer1M"] = "30.00"
        });

        // 1000 input @ $6.00/1M + 500 output @ $30.00/1M
        // = 0.006 + 0.015 = 0.021
        var cost = estimator.EstimateCost("claude-sonnet-4-6", 1000, 500);

        Assert.Equal(0.021m, cost);
    }

    [Fact]
    public void EstimateCost_FallsBackToDefaults_WhenConfigMissing()
    {
        // No config keys for this model — should use defaults
        var estimator = CreateEstimator();

        var cost = estimator.EstimateCost("claude-sonnet-4-6", 2_000_000, 0);

        // 2M input @ $3.00/1M = $6.00
        Assert.Equal(6.00m, cost);
    }

    [Fact]
    public void EstimateCost_ZeroTokens_ReturnsZero()
    {
        var estimator = CreateEstimator();

        var cost = estimator.EstimateCost("claude-sonnet-4-6", 0, 0);

        Assert.Equal(0m, cost);
    }
}
