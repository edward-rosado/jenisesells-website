using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GDrive;

public static class GDriveClientExtensions
{
    /// <summary>
    /// Registers <see cref="IGDriveClient"/> using the internal <see cref="GDriveApiClient"/> implementation.
    /// Requires <see cref="IOAuthRefresher"/> to be registered first.
    /// </summary>
    public static IServiceCollection AddGDriveClient(this IServiceCollection services)
    {
        services.AddSingleton<IGDriveClient>(sp =>
            new GDriveApiClient(
                sp.GetRequiredService<IOAuthRefresher>(),
                sp.GetRequiredService<ILogger<GDriveApiClient>>()));
        return services;
    }
}
