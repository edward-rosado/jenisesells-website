using Microsoft.Extensions.Configuration;

namespace RealEstateStar.Api.Services;

public class LlmCostEstimator
{
    private readonly IConfiguration _config;

    public LlmCostEstimator(IConfiguration config) => _config = config;

    public decimal EstimateCost(string model, int inputTokens, int outputTokens)
    {
        var inputPer1M = _config.GetValue<decimal>($"LlmPricing:{model}:InputPer1M", GetDefaultInputPrice(model));
        var outputPer1M = _config.GetValue<decimal>($"LlmPricing:{model}:OutputPer1M", GetDefaultOutputPrice(model));

        return (inputTokens * inputPer1M / 1_000_000m) + (outputTokens * outputPer1M / 1_000_000m);
    }

    private static decimal GetDefaultInputPrice(string model) => model switch
    {
        "claude-sonnet-4-6" => 3.00m,
        "claude-haiku-4-5" => 0.80m,
        _ => 3.00m
    };

    private static decimal GetDefaultOutputPrice(string model) => model switch
    {
        "claude-sonnet-4-6" => 15.00m,
        "claude-haiku-4-5" => 4.00m,
        _ => 15.00m
    };
}
