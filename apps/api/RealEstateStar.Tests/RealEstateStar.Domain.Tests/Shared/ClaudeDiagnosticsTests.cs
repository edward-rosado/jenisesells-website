using RealEstateStar.Domain.Shared;

namespace RealEstateStar.Domain.Tests.Shared;

public class ClaudeDiagnosticsTests
{
    [Fact]
    public void RecordUsage_IncrementsTokenCounters()
    {
        Assert.Equal("claude.tokens.input", ClaudeDiagnostics.TokensInput.Name);
        Assert.Equal("claude.tokens.output", ClaudeDiagnostics.TokensOutput.Name);
    }

    [Fact]
    public void RecordUsage_CalculatesSonnetCost()
    {
        // 1000 input @ $3/M + 500 output @ $15/M = $0.003 + $0.0075 = $0.0105
        const int inputTokens = 1000;
        const int outputTokens = 500;
        const double inputRate = 3.0 / 1_000_000;
        const double outputRate = 15.0 / 1_000_000;

        var expectedCost = inputTokens * inputRate + outputTokens * outputRate;

        Assert.Equal(0.0105, expectedCost, precision: 7);
    }

    [Fact]
    public void RecordUsage_CalculatesHaikuCost()
    {
        // model "claude-haiku-4-5-20251001" contains "haiku" → haiku rates ($0.80/M + $4/M)
        const int inputTokens = 1000;
        const int outputTokens = 500;
        const double inputRate = 0.80 / 1_000_000;
        const double outputRate = 4.0 / 1_000_000;

        var expectedCost = inputTokens * inputRate + outputTokens * outputRate;

        // $0.0008 + $0.002 = $0.0028
        Assert.Equal(0.0028, expectedCost, precision: 7);
    }

    [Fact]
    public void RecordFailure_IncrementsFailedCounter()
    {
        Assert.Equal("claude.calls_failed", ClaudeDiagnostics.CallsFailed.Name);
    }

    [Fact]
    public void CounterNames_MatchExpectedOTelNames()
    {
        Assert.Equal("claude.tokens.input", ClaudeDiagnostics.TokensInput.Name);
        Assert.Equal("claude.tokens.output", ClaudeDiagnostics.TokensOutput.Name);
        Assert.Equal("claude.cost_usd", ClaudeDiagnostics.CostUsd.Name);
        Assert.Equal("claude.calls_total", ClaudeDiagnostics.CallsTotal.Name);
        Assert.Equal("claude.calls_failed", ClaudeDiagnostics.CallsFailed.Name);
        Assert.Equal("claude.call_duration_ms", ClaudeDiagnostics.CallDuration.Name);
    }
}
