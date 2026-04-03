using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Activities.Lead.Persist;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Workers.Lead.Orchestrator;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers per-lead orchestrator compute services: <see cref="ILeadScorer"/> and
    /// <see cref="PersistActivity"/>.
    /// The Channel-based <c>LeadOrchestrator</c> BackgroundService and <c>LeadOrchestratorChannel</c>
    /// were removed in Phase 4; Azure Durable Functions now orchestrate lead processing via
    /// <c>LeadOrchestratorFunction</c>.
    /// </summary>
    public static IServiceCollection AddLeadOrchestrator(this IServiceCollection services)
    {
        services.AddSingleton<ILeadScorer, LeadScorer>();
        services.AddSingleton<PersistActivity>();
        return services;
    }
}
