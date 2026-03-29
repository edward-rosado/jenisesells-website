using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Activation.Interfaces;

namespace RealEstateStar.Services.AgentConfig;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentConfigService(this IServiceCollection services)
    {
        services.AddSingleton<IAgentConfigService, AgentConfigService>();
        return services;
    }
}
