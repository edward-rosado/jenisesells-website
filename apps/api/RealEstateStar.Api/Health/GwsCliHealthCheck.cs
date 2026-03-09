using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

public class GwsCliHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("gws")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return HealthCheckResult.Unhealthy("gws CLI not found");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            await process.WaitForExitAsync(timeoutCts.Token);
            return process.ExitCode == 0
                ? HealthCheckResult.Healthy("gws CLI available")
                : HealthCheckResult.Degraded("gws CLI returned non-zero exit code");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("gws CLI health check timed out");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("gws CLI check failed", ex);
        }
    }
}
