using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Cma.Interfaces;

namespace RealEstateStar.Workers.Shared.Pdf;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CmaPdfGenerator"/> as <see cref="ICmaPdfGenerator"/>
    /// and <see cref="PdfActivity"/> as a scoped service.
    /// </summary>
    public static IServiceCollection AddPdfService(this IServiceCollection services)
    {
        services.AddSingleton<ICmaPdfGenerator, CmaPdfGenerator>();
        services.AddTransient<PdfActivity>();
        return services;
    }
}
