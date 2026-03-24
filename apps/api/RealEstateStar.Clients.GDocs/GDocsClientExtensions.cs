using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GDocs;

public static class GDocsClientExtensions
{
    /// <summary>
    /// Registers <see cref="IGDocsClient"/> using the internal <see cref="GDocsApiClient"/> implementation.
    /// Requires <see cref="IOAuthRefresher"/> to be registered first.
    /// </summary>
    public static IServiceCollection AddGDocsClient(this IServiceCollection services)
    {
        services.AddSingleton<IGDocsClient>(sp =>
            new GDocsApiClient(
                sp.GetRequiredService<IOAuthRefresher>(),
                sp.GetRequiredService<ILogger<GDocsApiClient>>()));
        return services;
    }
}
