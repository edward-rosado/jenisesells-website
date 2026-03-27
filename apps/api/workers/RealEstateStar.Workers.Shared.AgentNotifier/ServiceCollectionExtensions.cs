namespace RealEstateStar.Workers.Shared.AgentNotifier;

using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Leads.Interfaces;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentNotifier(this IServiceCollection services)
    {
        services.AddScoped<IAgentNotifier, AgentNotificationService>();
        return services;
    }
}
