using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RealEstateStar.Workers.Shared.DependencyInjection;

/// <summary>
/// Extension methods for registering worker pipelines (channel + hosted service) in DI.
/// </summary>
public static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers a processing channel as a singleton and its worker as a hosted service.
    /// </summary>
    public static IServiceCollection AddWorkerPipeline<TChannel, TWorker>(this IServiceCollection services)
        where TChannel : class
        where TWorker : class, IHostedService
    {
        services.AddSingleton<TChannel>();
        services.AddHostedService<TWorker>();
        return services;
    }
}
