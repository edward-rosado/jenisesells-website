using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Lead.CMA;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers CMA pipeline compute services: <see cref="RentCastCompSource"/>, <see cref="ICompSource"/>,
    /// <see cref="ICompAggregator"/>, and <see cref="ICmaAnalyzer"/>.
    /// The Channel-based <c>CmaProcessingWorker</c> BackgroundService was removed in Phase 4;
    /// Azure Durable Functions now orchestrate CMA processing via <c>CmaProcessingFunction</c>.
    /// </summary>
    public static IServiceCollection AddCmaPipeline(this IServiceCollection services)
    {
        services.AddSingleton<RentCastCompSource>();
        services.AddSingleton<ICompSource>(sp => sp.GetRequiredService<RentCastCompSource>());
        services.AddSingleton<ICompAggregator>(sp =>
            new CompAggregator(
                sp.GetServices<ICompSource>(),
                sp.GetRequiredService<ILogger<CompAggregator>>()));
        services.AddSingleton<ICmaAnalyzer>(sp =>
            new ClaudeCmaAnalyzer(
                sp.GetRequiredService<IAnthropicClient>(),
                sp.GetRequiredService<ILogger<ClaudeCmaAnalyzer>>()));
        return services;
    }
}
