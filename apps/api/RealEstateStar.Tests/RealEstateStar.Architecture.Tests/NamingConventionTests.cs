using System.Reflection;
using FluentAssertions;

namespace RealEstateStar.Architecture.Tests;

/// <summary>
/// Enforces naming conventions across all projects. When a test fails, the violating type
/// is not conforming to the layer's naming contract. To resolve: rename the type (preferred),
/// or add it to the excluded list below with a TODO comment tracking the cleanup.
/// </summary>
public class NamingConventionTests
{
    // ---------------------------------------------------------------------------
    // Rule 1: DataServices — public classes must end with DataService or Decorator
    // ---------------------------------------------------------------------------

    // TODO(naming-cleanup): These DataServices types violate the naming convention.
    // They are excluded here to keep CI green while the rename task is tracked separately.
    private static readonly HashSet<string> DataServicesExcluded = new()
    {
        // Ends with Store instead of DataService — should be renamed to *DataService
        "RealEstateStar.DataServices.Leads.FileLeadStore",
        "RealEstateStar.DataServices.Storage.NullTokenStore",
        "RealEstateStar.DataServices.WhatsApp.WhatsAppIdempotencyStore",

        // Ends with Provider — storage providers co-located in DataServices for routing
        "RealEstateStar.DataServices.Leads.LocalStorageProvider",
        "RealEstateStar.DataServices.Privacy.LocalComplianceStorageProvider",
        "RealEstateStar.DataServices.Storage.FanOutStorageProvider",

        // Ends with Service (not DataService) — should be renamed to *DataService
        "RealEstateStar.DataServices.Onboarding.ProfileScraperService",
        "RealEstateStar.DataServices.Privacy.ConsentAuditService",
        "RealEstateStar.DataServices.Privacy.NullConsentAuditService",
        "RealEstateStar.DataServices.WhatsApp.DisabledWebhookQueueService",
        "RealEstateStar.DataServices.WhatsApp.DisabledWhatsAppAuditService",
        "RealEstateStar.DataServices.WhatsApp.AzureWebhookQueueService",
        "RealEstateStar.DataServices.WhatsApp.AzureWhatsAppAuditService",

        // No qualifying suffix — should be renamed
        "RealEstateStar.DataServices.Privacy.DriveChangeMonitor",
        "RealEstateStar.DataServices.WhatsApp.ConversationLogger",
    };

    [Fact]
    public void DataServices_PublicClasses_MustEndWith_DataService_OrDecorator()
    {
        var assembly = typeof(DataServices.Leads.LeadDataService).Assembly;
        var violations = assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            // Exclusions: infrastructure/config helpers that are not storage-routing classes
            .Where(t => !t.Name.EndsWith("Extensions"))
            .Where(t => !t.Name.EndsWith("Options"))
            .Where(t => !t.Name.EndsWith("Entry"))
            .Where(t => !t.Name.EndsWith("Data"))
            // Static utility classes have no instance and are not routing classes
            .Where(t => !IsStaticClass(t))
            // Records are value types / DTOs, not service classes
            .Where(t => !IsRecord(t))
            .Where(t => !DataServicesExcluded.Contains(t.FullName!))
            .Where(t => !t.Name.EndsWith("DataService") && !t.Name.EndsWith("Decorator"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all public classes in DataServices must end with 'DataService' or 'Decorator' " +
            "(the storage-routing layer naming contract). " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 2: Data — public classes must end with Provider, Store, or Factory
    // ---------------------------------------------------------------------------

    [Fact]
    public void Data_PublicClasses_MustEndWith_Provider_Store_OrFactory()
    {
        var assembly = typeof(Data.LocalFileStorageProvider).Assembly;
        var violations = assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !t.Name.EndsWith("Extensions"))
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t => !t.Name.EndsWith("Provider")
                     && !t.Name.EndsWith("Store")
                     && !t.Name.EndsWith("Factory"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all public classes in Data must end with 'Provider', 'Store', or 'Factory' " +
            "(the raw I/O layer naming contract). " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 3: Activities — public classes must end with Activity
    // ---------------------------------------------------------------------------

    // TODO(naming-cleanup): CmaPdfGenerator lives in Activities.Pdf but its name
    // describes a generator, not an activity. It should either be renamed to
    // CmaPdfActivity or moved to a Generators/ subfolder with an appropriate rule carve-out.
    private static readonly HashSet<string> ActivitiesExcluded = new()
    {
        "RealEstateStar.Activities.Pdf.CmaPdfGenerator",
    };

    [Fact]
    public void Activities_PublicClasses_MustEndWith_Activity()
    {
        var activitiesAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Activities.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .ToList();

        activitiesAssemblies.Should().NotBeEmpty("at least one Activities.* assembly must be loaded");

        var violations = activitiesAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !t.Name.EndsWith("Extensions"))
            .Where(t => !t.Name.EndsWith("Diagnostics"))
            // Generator suffix exclusion: implementations that generate artifacts (not pipeline steps)
            .Where(t => !t.Name.EndsWith("Generator"))
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t => !ActivitiesExcluded.Contains(t.FullName!))
            .Where(t => !t.Name.EndsWith("Activity"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all public classes in Activities.* must end with 'Activity' " +
            "(the pipeline-step naming contract). " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 4: Workers — public classes must end with Worker, Channel, Scorer, or Orchestrator
    // ---------------------------------------------------------------------------

    // TODO(naming-cleanup): These Workers types violate the naming convention.
    // They are excluded here to keep CI green while the rename task is tracked separately.
    private static readonly HashSet<string> WorkersExcluded = new()
    {
        // ConversationHandler, NoopIntentClassifier, NoopResponseGenerator are
        // implementation classes for domain interfaces — should be renamed or moved to Services.*
        "RealEstateStar.Workers.WhatsApp.ConversationHandler",
        "RealEstateStar.Workers.WhatsApp.NoopIntentClassifier",
        "RealEstateStar.Workers.WhatsApp.NoopResponseGenerator",

        // WhatsAppRetryJob is a scheduled task, not a worker — consider renaming to *Worker
        "RealEstateStar.Workers.WhatsApp.WhatsAppRetryJob",
    };

    [Fact]
    public void Workers_PublicClasses_MustEndWith_Worker_Channel_Scorer_OrOrchestrator()
    {
        var workerAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Workers.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            // Workers.Shared contains base classes, not pipeline implementations
            .Where(a => a.GetName().Name != "RealEstateStar.Workers.Shared")
            .ToList();

        workerAssemblies.Should().NotBeEmpty("at least one Workers.* assembly (excluding Shared) must be loaded");

        var violations = workerAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !t.Name.EndsWith("Extensions"))
            .Where(t => !t.Name.EndsWith("Diagnostics"))
            // Context, Source, Aggregator, Analyzer, Provider are support types within worker assemblies
            .Where(t => !t.Name.EndsWith("Context"))
            .Where(t => !t.Name.EndsWith("Source"))
            .Where(t => !t.Name.EndsWith("Aggregator"))
            .Where(t => !t.Name.EndsWith("Analyzer"))
            .Where(t => !t.Name.EndsWith("Provider"))
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t => !WorkersExcluded.Contains(t.FullName!))
            .Where(t => !t.Name.EndsWith("Worker")
                     && !t.Name.EndsWith("Channel")
                     && !t.Name.EndsWith("Scorer")
                     && !t.Name.EndsWith("Orchestrator"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all public classes in Workers.* (excluding Workers.Shared) must end with " +
            "'Worker', 'Channel', 'Scorer', or 'Orchestrator' (the pipeline naming contract). " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 5: Services — public classes must end with Service
    // ---------------------------------------------------------------------------

    [Fact]
    public void Services_PublicClasses_MustEndWith_Service()
    {
        var serviceAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Services.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .ToList();

        serviceAssemblies.Should().NotBeEmpty("at least one Services.* assembly must be loaded");

        var violations = serviceAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !t.Name.EndsWith("Extensions"))
            .Where(t => !t.Name.EndsWith("Diagnostics"))
            // Template and Drafter are implementation helpers within service assemblies
            .Where(t => !t.Name.EndsWith("Template"))
            .Where(t => !t.Name.EndsWith("Drafter"))
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t => !t.Name.EndsWith("Service"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all public classes in Services.* must end with 'Service'. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 6: Domain interfaces must start with I
    // ---------------------------------------------------------------------------

    [Fact]
    public void Domain_PublicInterfaces_MustStartWith_I()
    {
        var assembly = typeof(Domain.Leads.Models.Lead).Assembly;
        var violations = assembly.GetExportedTypes()
            .Where(t => t.IsInterface)
            .Where(t => !t.Name.StartsWith("I"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all public interfaces in Domain must start with 'I' (the interface naming convention). " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 7: Domain must contain no concrete implementations (only models, records, enums, static helpers, exceptions)
    // ---------------------------------------------------------------------------

    // TODO(naming-cleanup): These Domain types are concrete implementations that belong
    // in a lower layer (DataServices, Workers, or Services). They are excluded here while
    // the architectural cleanup is tracked separately.
    private static readonly HashSet<string> DomainImplementationsExcluded = new()
    {
        // OnboardingSession is a stateful model with mutable list fields — should move to Services.*
        "RealEstateStar.Domain.Onboarding.Models.OnboardingSession",

        // SystemDnsResolver implements IDnsResolver — should move to a Clients.* or Services.* project
        "RealEstateStar.Domain.Onboarding.Interfaces.SystemDnsResolver",
    };

    [Fact]
    public void Domain_MustNotContain_ConcreteImplementations()
    {
        var assembly = typeof(Domain.Leads.Models.Lead).Assembly;
        var violations = assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            // Static helper/utility classes (Diagnostics, Paths, Renderers, Parsers) are acceptable
            // as pure functions with no external dependencies
            .Where(t => !IsStaticClass(t))
            // Records are value-type models / DTOs — acceptable in Domain
            .Where(t => !IsRecord(t))
            // Exception types are part of the domain error contract
            .Where(t => !IsExceptionType(t))
            // Plain model classes (AccountConfig, Lead, etc.) are domain models, not implementations
            .Where(t => !IsPlainModelClass(t, assembly))
            .Where(t => !DomainImplementationsExcluded.Contains(t.FullName!))
            // A class is a "concrete implementation" if it implements at least one domain interface
            .Where(t => t.GetInterfaces().Any(i => i.Assembly == assembly && i.IsPublic))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Domain must not contain concrete implementations of its own interfaces. " +
            "Implementations belong in DataServices, Services.*, Workers.*, or Clients.*. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Returns true if the type is a static class (abstract + sealed at the IL level).</summary>
    private static bool IsStaticClass(Type t) => t.IsAbstract && t.IsSealed;

    /// <summary>Returns true if the type is a C# record (compiler-generated clone method).</summary>
    private static bool IsRecord(Type t) => t.GetMethod("<Clone>$") is not null;

    /// <summary>Returns true if the type derives from System.Exception.</summary>
    private static bool IsExceptionType(Type t) => typeof(Exception).IsAssignableFrom(t);

    /// <summary>
    /// Returns true if the type is a plain model/config class (i.e., it implements no
    /// interfaces from the same assembly). These are data-transfer types, not service implementations.
    /// </summary>
    private static bool IsPlainModelClass(Type t, Assembly domainAssembly) =>
        !t.GetInterfaces().Any(i => i.Assembly == domainAssembly && i.IsPublic);
}
