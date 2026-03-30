using Microsoft.Extensions.DependencyInjection;

namespace RealEstateStar.Activities.Activation.BrandMerge;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBrandMergeActivity(this IServiceCollection services)
    {
        services.AddTransient<BrandMergeActivity>();
        return services;
    }
}
