using Microsoft.Extensions.DependencyInjection;

namespace RealEstateStar.Activities.Activation.PersistAgentProfile;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistAgentProfileActivity(this IServiceCollection services)
    {
        services.AddTransient<AgentProfilePersistActivity>();
        return services;
    }
}
