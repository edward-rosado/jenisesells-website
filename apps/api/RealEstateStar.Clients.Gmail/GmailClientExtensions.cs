using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Gmail;

public static class GmailClientExtensions
{
    /// <summary>
    /// Registers <see cref="IGmailSender"/> using the internal <see cref="GmailApiClient"/> implementation.
    /// Requires <see cref="IOAuthRefresher"/> to be registered first.
    /// </summary>
    public static IServiceCollection AddGmailSender(this IServiceCollection services)
    {
        services.AddSingleton<IGmailSender>(sp =>
            new GmailApiClient(
                sp.GetRequiredService<IOAuthRefresher>(),
                sp.GetRequiredService<ILogger<GmailApiClient>>()));
        return services;
    }
}
