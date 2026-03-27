using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.DataServices.Leads;

namespace RealEstateStar.Api.Health;

public sealed class GoogleDriveHealthCheck(IDocumentStorageProvider fileStorageProvider) : IHealthCheck
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
