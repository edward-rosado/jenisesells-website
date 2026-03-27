using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Workers.Leads;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeadPipeline(this IServiceCollection services)
    {
        services.AddSingleton<LeadOrchestratorChannel>();
        services.AddSingleton<PdfProcessingChannel>();
        services.AddSingleton<ILeadScorer, LeadScorer>();
        services.AddSingleton<ILeadEmailDrafter, LeadEmailDrafter>();
        services.AddSingleton<IAgentNotifier, AgentNotifier>();
        services.AddSingleton<ICmaPdfGenerator, CmaPdfGenerator>();
        services.AddHostedService<LeadOrchestrator>();
        services.AddHostedService<PdfWorker>();
        return services;
    }
}
