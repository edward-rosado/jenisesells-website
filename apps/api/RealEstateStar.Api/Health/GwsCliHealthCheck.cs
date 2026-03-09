using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

public class GwsCliHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
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
                return Task.FromResult(HealthCheckResult.Unhealthy("gws CLI not found"));

            process.WaitForExit(5000);
            return Task.FromResult(process.ExitCode == 0
                ? HealthCheckResult.Healthy("gws CLI available")
                : HealthCheckResult.Degraded("gws CLI returned non-zero exit code"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("gws CLI check failed", ex));
        }
    }
}
