using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.Anthropic;

public static class ClaudeDiagnostics
{
    public const string ServiceName = "RealEstateStar.Claude";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> TokensInput = Meter.CreateCounter<long>(
        "claude.tokens.input", description: "Claude API input tokens consumed");
    public static readonly Counter<long> TokensOutput = Meter.CreateCounter<long>(
        "claude.tokens.output", description: "Claude API output tokens consumed");
    public static readonly Counter<double> CostUsd = Meter.CreateCounter<double>(
        "claude.cost_usd", unit: "USD", description: "Estimated Claude API cost");
    public static readonly Counter<long> CallsTotal = Meter.CreateCounter<long>(
        "claude.calls_total", description: "Total Claude API calls");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>(
        "claude.calls_failed", description: "Failed Claude API calls");
    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>(
        "claude.call_duration_ms", description: "Claude API call duration in milliseconds");

    public static void RecordUsage(string pipeline, string model, int inputTokens, int outputTokens, double durationMs)
    {
        var tags = new TagList { { "pipeline", pipeline }, { "model", model } };

        TokensInput.Add(inputTokens, tags);
        TokensOutput.Add(outputTokens, tags);
        CallsTotal.Add(1, tags);
        CallDuration.Record(durationMs, tags);

        var (inputRate, outputRate) = model switch
        {
            var m when m.Contains("haiku") => (0.80 / 1_000_000, 4.0 / 1_000_000),
            _ => (3.0 / 1_000_000, 15.0 / 1_000_000)
        };
        CostUsd.Add(inputTokens * inputRate + outputTokens * outputRate, tags);
    }

    public static void RecordFailure(string pipeline, string model)
    {
        CallsFailed.Add(1, new TagList { { "pipeline", pipeline }, { "model", model } });
    }
}
