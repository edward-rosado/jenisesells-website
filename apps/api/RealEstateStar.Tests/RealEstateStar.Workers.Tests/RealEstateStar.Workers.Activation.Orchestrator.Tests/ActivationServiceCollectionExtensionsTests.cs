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
    [Fact]
    public void AddActivationPipeline_RegistersAllFifteenWorkers()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddActivationPipeline();

        // Verify all 15 worker types are registered by inspecting descriptors
        // (not resolving instances — workers have external dependencies not registered here)
        var registeredTypes = serviceCollection
            .Select(d => d.ServiceType)
            .ToHashSet();

        // Phase 1: gather workers
        registeredTypes.Should().Contain(typeof(AgentEmailFetchWorker));
        registeredTypes.Should().Contain(typeof(DriveIndexWorker));
        registeredTypes.Should().Contain(typeof(AgentDiscoveryWorker));

        // Phase 2: synthesis workers
        registeredTypes.Should().Contain(typeof(VoiceExtractionWorker));
        registeredTypes.Should().Contain(typeof(PersonalityWorker));
        registeredTypes.Should().Contain(typeof(BrandingDiscoveryWorker));
        registeredTypes.Should().Contain(typeof(CmaStyleWorker));
        registeredTypes.Should().Contain(typeof(MarketingStyleWorker));
        registeredTypes.Should().Contain(typeof(WebsiteStyleWorker));
        registeredTypes.Should().Contain(typeof(PipelineAnalysisWorker));
        registeredTypes.Should().Contain(typeof(CoachingWorker));
        registeredTypes.Should().Contain(typeof(BrandExtractionWorker));
        registeredTypes.Should().Contain(typeof(BrandVoiceWorker));
        registeredTypes.Should().Contain(typeof(ComplianceAnalysisWorker));
        registeredTypes.Should().Contain(typeof(FeeStructureWorker));
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
