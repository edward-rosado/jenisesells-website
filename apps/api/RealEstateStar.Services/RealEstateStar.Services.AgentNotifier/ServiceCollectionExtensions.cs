using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Services.AgentNotifier;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentNotifier(this IServiceCollection services)
    {
        services.AddSingleton<IAgentNotifier, AgentNotifierService>();
        return services;
    }
}
