using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Activation.Interfaces;

namespace RealEstateStar.Services.BrandMerge;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBrandMergeService(this IServiceCollection services)
    {
        services.AddSingleton<IBrandMergeService, BrandMergeService>();
        return services;
    }
}
