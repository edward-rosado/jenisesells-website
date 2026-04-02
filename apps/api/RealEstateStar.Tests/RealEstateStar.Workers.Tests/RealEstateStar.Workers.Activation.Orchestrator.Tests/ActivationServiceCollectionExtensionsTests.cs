using FluentAssertions;
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
using RealEstateStar.Workers.Activation.FeeStructure;
using RealEstateStar.Workers.Activation.MarketingStyle;
using RealEstateStar.Workers.Activation.Personality;
using RealEstateStar.Workers.Activation.PipelineAnalysis;
using RealEstateStar.Workers.Activation.VoiceExtraction;
using RealEstateStar.Workers.Activation.WebsiteStyle;

namespace RealEstateStar.Workers.Activation.Orchestrator.Tests;

/// <summary>
/// Verifies that <see cref="ActivationServiceCollectionExtensions.AddActivationPipeline"/>
/// registers all 15 worker types as transient services without registering any BackgroundService
/// (removed in Phase 4 — Azure Durable Functions now orchestrate activation).
/// </summary>
public class ActivationServiceCollectionExtensionsTests
{
    private static IServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddActivationPipeline()
            .BuildServiceProvider();

    [Fact]
    public void AddActivationPipeline_RegistersAllFifteenWorkers()
    {
        var services = BuildServices();

        // Phase 1: gather workers
        services.GetRequiredService<AgentEmailFetchWorker>().Should().NotBeNull();
        services.GetRequiredService<DriveIndexWorker>().Should().NotBeNull();
        services.GetRequiredService<AgentDiscoveryWorker>().Should().NotBeNull();

        // Phase 2: synthesis workers
        services.GetRequiredService<VoiceExtractionWorker>().Should().NotBeNull();
        services.GetRequiredService<PersonalityWorker>().Should().NotBeNull();
        services.GetRequiredService<BrandingDiscoveryWorker>().Should().NotBeNull();
        services.GetRequiredService<CmaStyleWorker>().Should().NotBeNull();
        services.GetRequiredService<MarketingStyleWorker>().Should().NotBeNull();
        services.GetRequiredService<WebsiteStyleWorker>().Should().NotBeNull();
        services.GetRequiredService<PipelineAnalysisWorker>().Should().NotBeNull();
        services.GetRequiredService<CoachingWorker>().Should().NotBeNull();
        services.GetRequiredService<BrandExtractionWorker>().Should().NotBeNull();
        services.GetRequiredService<BrandVoiceWorker>().Should().NotBeNull();
        services.GetRequiredService<ComplianceAnalysisWorker>().Should().NotBeNull();
        services.GetRequiredService<FeeStructureWorker>().Should().NotBeNull();
    }

    [Fact]
    public void AddActivationPipeline_DoesNotRegisterAnyHostedService()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddActivationPipeline();

        // Phase 4: BackgroundService (ActivationOrchestrator) was removed.
        // Verify no IHostedService is registered by AddActivationPipeline.
        // IHostedService lives in Microsoft.Extensions.Hosting.Abstractions which is
        // transitively available via Microsoft.Extensions.DependencyInjection.
        var hostedServiceType = typeof(Microsoft.Extensions.Hosting.IHostedService);
        var hostedServices = serviceCollection
            .Where(d => d.ServiceType == hostedServiceType)
            .ToList();

        hostedServices.Should().BeEmpty(
            "BackgroundService orchestrator was removed in Phase 4; " +
            "Azure Durable Functions now handle activation orchestration");
    }

    [Fact]
    public void WorkerTypes_AreRegisteredAsTransient()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddActivationPipeline();

        var workerDescriptors = serviceCollection
            .Where(d => d.ServiceType == typeof(AgentEmailFetchWorker) ||
                        d.ServiceType == typeof(DriveIndexWorker) ||
                        d.ServiceType == typeof(AgentDiscoveryWorker))
            .ToList();

        workerDescriptors.Should().AllSatisfy(d =>
            d.Lifetime.Should().Be(ServiceLifetime.Transient));
    }
}
