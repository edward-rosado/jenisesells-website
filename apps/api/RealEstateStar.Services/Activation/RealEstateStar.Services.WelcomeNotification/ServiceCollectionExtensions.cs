using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Activation.Interfaces;

namespace RealEstateStar.Services.WelcomeNotification;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWelcomeNotificationService(this IServiceCollection services)
    {
        services.AddSingleton<IWelcomeNotificationService, WelcomeNotificationService>();
        return services;
    }
}
