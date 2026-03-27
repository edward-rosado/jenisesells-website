using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Notifications.Interfaces;
using RealEstateStar.Notifications.Templates;

namespace RealEstateStar.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        // Email template rendering (privacy footer with unsubscribe/view-data links)
        services.AddSingleton<IEmailTemplateRenderer, PrivacyFooterRenderer>();

        return services;
    }
}
