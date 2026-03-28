using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Services.LeadCommunicator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeadCommunicator(this IServiceCollection services)
    {
        services.AddSingleton<ILeadEmailDrafter, LeadEmailDrafter>();
        services.AddSingleton<ILeadCommunicationService, LeadCommunicationService>();
        return services;
    }
}
