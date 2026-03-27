using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Services;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Cma;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCmaPipeline(this IServiceCollection services)
    {
        services.AddSingleton<CmaProcessingChannel>();
        services.AddHostedService<CmaProcessingWorker>();
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
