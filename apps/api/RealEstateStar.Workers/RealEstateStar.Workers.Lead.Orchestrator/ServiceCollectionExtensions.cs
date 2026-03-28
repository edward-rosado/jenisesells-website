using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Activities.Persist;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Workers.Lead.Orchestrator;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the per-lead orchestrator and its direct dependencies.
    /// Callers must also register CmaProcessingChannel, HomeSearchProcessingChannel,
    /// PdfActivity, LeadCommunicatorService, AgentNotifierService, and IContentCache
    /// separately (or via their own AddX() extension methods).
    /// </summary>
    public static IServiceCollection AddLeadOrchestrator(this IServiceCollection services)
    {
        services.AddSingleton<LeadOrchestratorChannel>();
        services.AddSingleton<ILeadScorer, LeadScorer>();
        services.AddSingleton<PersistActivity>();
        services.AddHostedService<LeadOrchestrator>();
        return services;
    }
}
