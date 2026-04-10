using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Workers.Activation.AgentDiscovery;
using RealEstateStar.Workers.Activation.BrandExtraction;
using RealEstateStar.Workers.Activation.BrandingDiscovery;
using RealEstateStar.Workers.Activation.BrandVoice;
using RealEstateStar.Workers.Activation.CmaStyle;
using RealEstateStar.Workers.Activation.Coaching;
using RealEstateStar.Workers.Activation.ComplianceAnalysis;
using RealEstateStar.Workers.Activation.DriveIndex;
using RealEstateStar.Workers.Activation.EmailFetch;
using RealEstateStar.Workers.Activation.EmailTransactionExtraction;
using RealEstateStar.Workers.Activation.FeeStructure;
using RealEstateStar.Workers.Activation.MarketingStyle;
using RealEstateStar.Workers.Activation.Personality;
using RealEstateStar.Workers.Activation.PipelineAnalysis;
using RealEstateStar.Workers.Activation.VoiceExtraction;
using RealEstateStar.Workers.Activation.WebsiteStyle;

namespace RealEstateStar.Workers.Activation.Orchestrator;

/// <summary>
/// DI wiring for the activation pipeline — all 15 workers (transient).
/// The BackgroundService orchestrator has been removed in Phase 4; Azure Durable Functions
/// now orchestrate these workers via <c>ActivationOrchestratorFunction</c>.
/// Call from Api/Program.cs; the orchestrator project already references all workers.
/// </summary>
public static class ActivationServiceCollectionExtensions
{
    public static IServiceCollection AddActivationPipeline(this IServiceCollection services)
    {
        // Phase 1: gather workers
        services.AddTransient<AgentEmailFetchWorker>();
        services.AddTransient<DriveIndexWorker>();
        services.AddTransient<AgentDiscoveryWorker>();
        services.AddTransient<EmailTransactionExtractor>();

        // Phase 2: synthesis workers
        services.AddTransient<VoiceExtractionWorker>();
        services.AddTransient<PersonalityWorker>();
        services.AddTransient<BrandingDiscoveryWorker>();
        services.AddTransient<CmaStyleWorker>();
        services.AddTransient<MarketingStyleWorker>();
        services.AddTransient<WebsiteStyleWorker>();
        services.AddTransient<PipelineAnalysisWorker>();
        services.AddTransient<CoachingWorker>();
        services.AddTransient<BrandExtractionWorker>();
        services.AddTransient<BrandVoiceWorker>();
        services.AddTransient<ComplianceAnalysisWorker>();
        services.AddTransient<FeeStructureWorker>();

        return services;
    }
}
