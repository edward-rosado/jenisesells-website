using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Lead.HomeSearch;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers home search pipeline compute services: <see cref="IHomeSearchProvider"/>.
    /// The Channel-based <c>HomeSearchProcessingWorker</c> BackgroundService was removed in Phase 4;
    /// Azure Durable Functions now orchestrate home search via <c>HomeSearchFunction</c>.
    /// </summary>
    public static IServiceCollection AddHomeSearchPipeline(this IServiceCollection services, IConfiguration configuration)
    {
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
