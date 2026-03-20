using System.Diagnostics;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken ct)
    {
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        using var process = new Process { StartInfo = startInfo };

        // Start timeout BEFORE reads so the entire operation is bounded
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }
}
