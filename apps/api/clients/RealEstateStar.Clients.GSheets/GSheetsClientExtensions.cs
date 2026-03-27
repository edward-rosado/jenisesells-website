using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GSheets;

public static class GSheetsClientExtensions
{
    /// <summary>
    /// Registers <see cref="IGSheetsClient"/> using the internal <see cref="GSheetsApiClient"/> implementation.
    /// Requires <see cref="IOAuthRefresher"/> to be registered first.
    /// </summary>
    public static IServiceCollection AddGSheetsClient(
        this IServiceCollection services,
        string clientId,
        string clientSecret)
    {
        services.AddSingleton<IGSheetsClient>(sp =>
            new GSheetsApiClient(
                sp.GetRequiredService<IOAuthRefresher>(),
                clientId,
                clientSecret,
                sp.GetRequiredService<ILogger<GSheetsApiClient>>()));
        return services;
    }
}
