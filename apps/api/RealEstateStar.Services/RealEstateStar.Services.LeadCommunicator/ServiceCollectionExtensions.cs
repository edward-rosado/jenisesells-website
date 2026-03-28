using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Notifications.Interfaces;
using RealEstateStar.Services.LeadCommunicator.Templates;

namespace RealEstateStar.Services.LeadCommunicator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeadCommunicator(this IServiceCollection services)
    {
        services.AddSingleton<ILeadEmailDrafter, LeadEmailDrafter>();
        services.AddSingleton<ILeadCommunicationService, LeadCommunicationService>();
        // Email template rendering (privacy footer with unsubscribe/view-data links)
        services.AddSingleton<IEmailTemplateRenderer, PrivacyFooterRenderer>();
        return services;
    }
}
