using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Lead.HomeSearch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHomeSearchPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<HomeSearchProcessingChannel>();
        services.AddHostedService<HomeSearchProcessingWorker>();

        var sources = configuration.GetSection("Pipeline:HomeSearch:Sources")
            .Get<Dictionary<string, string>>() ?? new();
        services.AddSingleton<IHomeSearchProvider>(sp =>
            new ScraperHomeSearchProvider(
                sp.GetRequiredService<IAnthropicClient>(),
                sp.GetRequiredService<IScraperClient>(),
                sources,
                sp.GetRequiredService<ILogger<ScraperHomeSearchProvider>>()));
        return services;
    }
}
