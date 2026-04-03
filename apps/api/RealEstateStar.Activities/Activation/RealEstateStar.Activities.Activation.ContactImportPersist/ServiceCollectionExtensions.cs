using Microsoft.Extensions.DependencyInjection;

namespace RealEstateStar.Activities.Activation.ContactImportPersist;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContactImportPersistActivity(this IServiceCollection services)
    {
        services.AddTransient<ContactImportPersistActivity>();
        return services;
    }
}
