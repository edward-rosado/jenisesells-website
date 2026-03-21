using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Api.Services.Storage;

namespace RealEstateStar.Api.Health;

public class GoogleDriveHealthCheck(IFileStorageProvider fileStorageProvider) : IHealthCheck
{
    private const string HealthCheckFolder = "_health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        try
        {
            await fileStorageProvider.ListDocumentsAsync(HealthCheckFolder, ct);
            return HealthCheckResult.Healthy("Google Drive reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Google Drive unreachable", ex);
        }
    }
}
