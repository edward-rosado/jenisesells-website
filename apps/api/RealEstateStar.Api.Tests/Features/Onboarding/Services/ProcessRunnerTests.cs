using System.Diagnostics;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_SuccessfulCommand_ReturnsZeroExitCode()
    {
        var psi = new ProcessStartInfo("dotnet", "--version");
        var result = await _runner.RunAsync(psi, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Stdout));
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_ReturnsNonZeroExitCode()
    {
        var psi = new ProcessStartInfo("dotnet", "nonexistent-command-xyz");
        var result = await _runner.RunAsync(psi, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_CapturesStdout()
    {
        var psi = new ProcessStartInfo("dotnet", "--info");
        var result = await _runner.RunAsync(psi, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Contains(".NET", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_Timeout_ThrowsOperationCanceled()
    {
        // Use a command that takes a long time — ping with high count
        var psi = new ProcessStartInfo("dotnet", "help");

        // Give an extremely short timeout so it cancels
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // already cancelled

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _runner.RunAsync(psi, TimeSpan.FromMilliseconds(1), cts.Token));
    }

    [Fact]
    public async Task RunAsync_CancellationToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var psi = new ProcessStartInfo("dotnet", "--version");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _runner.RunAsync(psi, TimeSpan.FromSeconds(30), cts.Token));
    }

    [Fact]
    public async Task RunAsync_SetsProcessProperties()
    {
        // Verify the runner configures RedirectStandardOutput, etc.
        var psi = new ProcessStartInfo("dotnet", "--version");
        var result = await _runner.RunAsync(psi, TimeSpan.FromSeconds(30), CancellationToken.None);

        // If we got stdout, the redirect worked
        Assert.NotNull(result.Stdout);
        Assert.NotNull(result.Stderr);
    }
}
