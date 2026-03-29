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
    public static IServiceCollection AddGmailSender(
        this IServiceCollection services,
        string clientId,
        string clientSecret)
    {
        services.AddSingleton<IGmailSender>(sp =>
            new GmailApiClient(
                sp.GetRequiredService<IOAuthRefresher>(),
                clientId,
                clientSecret,
                sp.GetRequiredService<ILogger<GmailApiClient>>()));
        return services;
    }

    /// <summary>
    /// Registers <see cref="IGmailReader"/> using the internal <see cref="GmailReaderClient"/> implementation.
    /// Requires <see cref="IOAuthRefresher"/> to be registered first.
    /// </summary>
    public static IServiceCollection AddGmailReader(
        this IServiceCollection services,
        string clientId,
        string clientSecret)
    {
        services.AddSingleton<IGmailReader>(sp =>
            new GmailReaderClient(
                sp.GetRequiredService<IOAuthRefresher>(),
                clientId,
                clientSecret,
                sp.GetRequiredService<ILogger<GmailReaderClient>>()));
        return services;
    }
}
