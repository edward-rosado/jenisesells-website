using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Workers.Lead.Orchestrator;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the per-lead orchestrator and its direct dependencies.
    /// Callers must also register CmaProcessingChannel, HomeSearchProcessingChannel,
    /// PdfActivity, LeadCommunicationService, and AgentNotificationService separately
    /// (or via their own AddX() extension methods).
    /// </summary>
    public static IServiceCollection AddLeadOrchestrator(this IServiceCollection services)
    {
        services.AddSingleton<LeadOrchestratorChannel>();
        services.AddSingleton<ILeadScorer, LeadScorer>();
        services.AddHostedService<LeadOrchestrator>();
        return services;
    }
}
