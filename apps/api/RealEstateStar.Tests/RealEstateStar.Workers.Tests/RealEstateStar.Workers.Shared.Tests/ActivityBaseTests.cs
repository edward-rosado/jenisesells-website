using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.Workers.Shared.Tests;

public class ActivityBaseTests
{
    private sealed class TestActivity : ActivityBase
    {
        public TestActivity() : base(
            new ActivitySource("Test"),
            NullLogger<TestActivity>.Instance,
            "test-activity") { }

        public bool WasExecuted { get; private set; }

        public Task RunAsync(CancellationToken ct) =>
            ExecuteWithSpanAsync("test-op", async () =>
            {
                WasExecuted = true;
                await Task.CompletedTask;
            }, ct);

        public Task<string> RunWithResultAsync(CancellationToken ct) =>
            ExecuteWithSpanAsync("test-op", async () =>
            {
                await Task.CompletedTask;
                return "result";
            }, ct);
    }

    [Fact]
    public async Task ExecuteWithSpanAsync_runs_action_and_logs()
    {
        var activity = new TestActivity();
        await activity.RunAsync(CancellationToken.None);
        Assert.True(activity.WasExecuted);
    }

    [Fact]
    public async Task ExecuteWithSpanAsync_returns_result()
    {
        var activity = new TestActivity();
        var result = await activity.RunWithResultAsync(CancellationToken.None);
        Assert.Equal("result", result);
    }

    [Fact]
    public async Task ExecuteWithSpanAsync_throws_on_cancellation()
    {
        var activity = new TestActivity();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => activity.RunAsync(cts.Token));
    }
}
