using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GooglePlaces;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGooglePlacesClient(this IServiceCollection services, IConfiguration configuration, ILogger startupLogger)
    {
        services.Configure<GooglePlacesOptions>(configuration.GetSection("GooglePlaces"));

        var apiKey = configuration["GooglePlaces:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            startupLogger.LogWarning(
                "[STARTUP-GPLACES] GooglePlaces:ApiKey is not configured. " +
                "Google Reviews will be unavailable. " +
                "Enable Places API (New) in Google Cloud Console and set GooglePlaces__ApiKey.");
        else
            startupLogger.LogInformation("[STARTUP-GPLACES] Google Places API configured (key present).");

        services.AddSingleton<IGoogleReviewsClient, GoogleReviewsClient>();
        services.AddHttpClient("GooglePlaces");
        return services;
    }
}
