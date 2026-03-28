using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Workers.Shared.AgentNotifier;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentNotifier(this IServiceCollection services)
    {
        services.AddSingleton<IAgentNotifier, AgentNotificationService>();
        return services;
    }
}
