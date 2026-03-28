using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Hosting;

namespace RealEstateStar.Architecture.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// PROJECT TAXONOMY — The authoritative source of truth for what each layer
// is allowed to contain, what it is forbidden from containing, and what it
// can depend on.
//
// Architecture:
//
//   Api             → Composition root: endpoints, DI wiring, middleware,
//                     health checks, Options. No business logic lives here.
//
//   Domain          → Pure contracts: interfaces (I*), models, records, enums,
//                     static utility helpers, and exception types.
//                     Zero external dependencies. Zero implementations.
//
//   Data            → Raw I/O primitives: *Provider, *Store, *Factory.
//                     Depends only on Domain.
//
//   DataServices    → Storage routing + business logic: *DataService, *Decorator.
//                     No endpoints, no workers, no activities.
//                     Depends on Domain (+ Data for cross-layer composition).
//
//   Activities.*    → Pipeline steps that compute + write one artifact: *Activity.
//                     No endpoints, no workers, no direct client calls.
//                     Depends on Domain + Workers.Shared.
//
//   Services.*      → Synchronous calls the orchestrator awaits: *Service.
//                     No endpoints, no background workers.
//                     Depends on Domain + Workers.Shared.
//
//   Workers.*       → Channel-based BackgroundServices: *Worker, *Channel,
//                     *Scorer, *Orchestrator. Pure compute pipelines.
//                     No endpoints, no direct client calls (resolved via DI).
//                     Depends on Domain + Workers.Shared.
//
//   Clients.*       → External API wrappers: *Client, *Sender, *Refresher.
//                     No endpoints, no workers, no services, no activities.
//                     Depends on Domain only (GoogleOAuth is the one shared
//                     infra client for Google API clients, same as Workers.Shared
//                     for Workers.*).
//
// Dependency flow (strict, enforced by csproj refs + DependencyTests.cs):
//   Api → everything (sole composition root)
//   Workers/Services/Activities → Domain + Workers.Shared
//   DataServices → Domain (+ Data for storage composition)
//   Data → Domain
//   Clients → Domain (+ GoogleOAuth for Google API clients)
//   Domain → nothing
//
// When a test fails: the violating type belongs in a different layer.
// Fix by moving the type and wiring it via DI in Api, OR add it to the
// appropriate exclusion set below with a TODO comment.
// ══════════════════════════════════════════════════════════════════════════════
public class ProjectTaxonomyTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static readonly Assembly ApiAssembly = typeof(global::Program).Assembly;
    private static readonly Assembly DomainAssembly = typeof(RealEstateStar.Domain.Leads.Models.Lead).Assembly;

    /// <summary>Returns true if the type is a static class (abstract + sealed at the IL level).</summary>
    private static bool IsStaticClass(Type t) => t.IsAbstract && t.IsSealed;

    /// <summary>Returns true if the type is a C# record (compiler-generated clone method).</summary>
    private static bool IsRecord(Type t) => t.GetMethod("<Clone>$") is not null;

    /// <summary>Returns true if the type derives from System.Exception.</summary>
    private static bool IsExceptionType(Type t) => typeof(Exception).IsAssignableFrom(t);

    /// <summary>
    /// Returns all exported public types from assemblies whose name matches the given prefix,
    /// excluding test and TestUtilities assemblies.
    /// </summary>
    private static IEnumerable<Type> GetProductionTypes(string assemblyPrefix) =>
        Directory.GetFiles(AppContext.BaseDirectory, $"{assemblyPrefix}*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"))
            .SelectMany(a => a.GetExportedTypes());

    /// <summary>
    /// Standard filter for "implementation class" candidates — excludes types that are
    /// definitionally not implementation classes across all layers.
    /// </summary>
    private static IEnumerable<Type> ImplementationClasses(IEnumerable<Type> types) =>
        types
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !t.Name.EndsWith("Extensions"))   // ServiceCollection extensions are not layer violations
            .Where(t => !t.Name.EndsWith("Options"))      // Options records are DI configuration, not implementations
            .Where(t => !t.Name.EndsWith("Diagnostics"))  // Diagnostics are infra glue, not layer violations
            .Where(t => !IsStaticClass(t))                // Static utility classes have no instance
            .Where(t => !IsRecord(t));                    // Records are value types / DTOs

    // ══════════════════════════════════════════════════════════════════════════
    // API LAYER — Composition Root
    //
    // Allowed:  *Endpoint, *Middleware, *HealthCheck, *Extensions, *Options
    // Forbidden: *Service, *Store, *Provider, *DataService, *Activity, *Worker,
    //            BackgroundService subclasses, Domain interface implementations
    // ══════════════════════════════════════════════════════════════════════════

    // All onboarding service classes have been migrated to Workers.Onboarding, Clients.Stripe,
    // and Clients.GoogleOAuth. No Api exclusions required.
    private static readonly HashSet<string> ApiServiceClassExclusions = new()
    {
    };

    [Fact]
    public void Api_MustNot_Contain_ServiceImplementations()
    {
        // *Service classes belong in Services.* or Workers.* projects, not in the composition root.
        var violations = ImplementationClasses(ApiAssembly.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Service"))
            .Where(t => !ApiServiceClassExclusions.Contains(t.FullName!))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — *Service classes belong in Services.* or Workers.* projects. " +
            "Api must only contain endpoints, middleware, infrastructure wiring, and health checks. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // TrialExpiryService has been migrated to Workers.Onboarding. No Api exclusions required.
    private static readonly HashSet<string> ApiBackgroundServiceExclusions = new()
    {
    };

    [Fact]
    public void Api_MustNot_Contain_BackgroundServices()
    {
        // BackgroundService subclasses are channel-based pipeline workers. They belong in
        // Workers.* projects where the pipeline is clearly separated from the HTTP boundary.
        var backgroundServiceType = typeof(BackgroundService);
        var hostedServiceType = typeof(IHostedService);

        var violations = ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => !ApiBackgroundServiceExclusions.Contains(t.FullName!))
            .Where(t =>
                backgroundServiceType.IsAssignableFrom(t) ||
                t.GetInterfaces().Any(i => i == hostedServiceType))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — BackgroundService and IHostedService implementations " +
            "belong in Workers.* projects. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Api_MustNot_Contain_StorageImplementations()
    {
        // Storage classes (*Store, *Provider, *DataService) belong in Data, DataServices, or Clients.
        var violations = ImplementationClasses(ApiAssembly.GetExportedTypes())
            .Where(t =>
                t.Name.EndsWith("Store") ||
                t.Name.EndsWith("Provider") ||
                t.Name.EndsWith("DataService"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — *Store, *Provider, and *DataService classes belong in " +
            "the Data, DataServices, or Clients layers. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Api_MustNot_Contain_ActivityImplementations()
    {
        // Pipeline step classes (*Activity) belong in Activities.* projects.
        var violations = ImplementationClasses(ApiAssembly.GetExportedTypes())
            .Where(t => t.Name.EndsWith("Activity"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — *Activity classes belong in Activities.* projects. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Api_EndpointClasses_MustEndWith_Endpoint()
    {
        // Every class implementing IEndpoint is an HTTP route handler under the REPR pattern.
        // The folder, type name, and operation name must match: GetStatus/ → GetStatusEndpoint.
        var iEndpointType = ApiAssembly.GetExportedTypes()
            .FirstOrDefault(t => t.IsInterface && t.Name == "IEndpoint");

        if (iEndpointType is null) return; // IEndpoint not found — skip gracefully

        var violations = ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Contains(iEndpointType))
            .Where(t => !t.Name.EndsWith("Endpoint"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all classes implementing IEndpoint must end with 'Endpoint' (REPR pattern naming contract). " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DOMAIN LAYER — Pure Contracts
    //
    // Allowed:  I* interfaces, model classes, records, enums, static helpers,
    //           exception types, *Diagnostics (ActivitySource/Meter singletons),
    //           *Paths, *Renderer, *Parser (pure functions, no external deps)
    // Forbidden: concrete implementations of Domain interfaces,
    //            System.Net.Http references
    //
    // NOTE: ClaudeDiagnostics in Domain exposes an ActivitySource. This is a
    // known exception — Diagnostics files are shared infra constants that are
    // pure (no external deps, no behavior). The rule below targets *non-static*
    // concrete implementation classes only.
    // ══════════════════════════════════════════════════════════════════════════

    // TODO: These concrete implementations in Domain violate the "pure contracts" rule.
    // Remove each entry as the type is moved to the appropriate layer.
    private static readonly HashSet<string> DomainConcreteImplementationExclusions = new()
    {
        // SystemDnsResolver implements IDnsResolver — wraps Dns.GetHostAddressesAsync.
        // Target: Clients.* or Services.Onboarding (external I/O wrapper).
        "RealEstateStar.Domain.Onboarding.Interfaces.SystemDnsResolver",

        // OnboardingSession is a stateful model with mutable list fields.
        // It is not a pure data-transfer record. Target: Services.Onboarding or Workers.Onboarding.
        "RealEstateStar.Domain.Onboarding.Models.OnboardingSession",
    };

    [Fact]
    public void Domain_MustNot_Contain_ConcreteImplementations()
    {
        // Domain is a pure contract layer. It may define interfaces, records, plain model classes,
        // enums, static utilities, exception types, and Diagnostics singletons — but it must not
        // contain classes that implement its own interfaces, because those are behaviors (not contracts).
        //
        // "Plain model class" = a class that implements no Domain-owned interfaces. Those are data
        // transfer types (e.g., Lead, AccountConfig) and are acceptable in Domain.

        var domainInterfaceSet = DomainAssembly.GetExportedTypes()
            .Where(t => t.IsInterface)
            .ToHashSet();

        var violations = DomainAssembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t => !IsExceptionType(t))
            .Where(t => !DomainConcreteImplementationExclusions.Contains(t.FullName!))
            // A class is a "concrete implementation" if it implements at least one public
            // Domain-owned interface. Plain model classes (no Domain interface) are acceptable.
            .Where(t => t.GetInterfaces().Any(i => domainInterfaceSet.Contains(i) && i.IsPublic))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Domain must not contain concrete implementations of its own interfaces. " +
            "Implementations belong in DataServices, Services.*, Workers.*, Activities.*, or Clients.*. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Domain_MustNot_Reference_SystemNetHttp()
    {
        // Domain has zero external dependencies. System.Net.Http (HttpClient) is infrastructure
        // that belongs in Clients.*, not in the pure contract layer.
        var referencedAssemblies = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("System.Net.Http"))
            .ToList();

        referencedAssemblies.Should().BeEmpty(
            "Domain must have zero external dependencies — System.Net.Http belongs in Clients.*. " +
            "Violations: {0}", string.Join(", ", referencedAssemblies));
    }

    [Fact]
    public void Domain_PublicInterfaces_MustStartWith_I()
    {
        // Every public interface in Domain must follow the standard I-prefix convention,
        // making interfaces immediately distinguishable from model types.
        var violations = DomainAssembly.GetExportedTypes()
            .Where(t => t.IsInterface)
            .Where(t => !t.Name.StartsWith("I"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all public interfaces in Domain must start with 'I' (the interface naming convention). " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DATA LAYER — Raw I/O Primitives
    //
    // Allowed:  *Provider, *Store, *Factory
    // Forbidden: *Service, *Worker, *Activity, *Endpoint
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Data_MustNot_Contain_ServiceOrWorkerClasses()
    {
        // Data is the raw I/O layer. It reads and writes bytes — it has no orchestration,
        // no pipeline steps, and no HTTP concerns.
        var dataAssembly = typeof(RealEstateStar.Data.LocalFileStorageProvider).Assembly;

        var violations = ImplementationClasses(dataAssembly.GetExportedTypes())
            .Where(t =>
                t.Name.EndsWith("Service") ||
                t.Name.EndsWith("Worker") ||
                t.Name.EndsWith("Activity") ||
                t.Name.EndsWith("Endpoint") ||
                t.Name.EndsWith("DataService"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Data is a raw I/O layer — it must not contain Service, Worker, Activity, or Endpoint classes. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DATASERVICES LAYER — Storage Routing + Business Logic
    //
    // Allowed:  *DataService, *Decorator (+ internal helpers)
    // Forbidden: *Endpoint, *Worker, *Activity, *Client
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DataServices_MustNot_Contain_EndpointOrWorkerOrActivityClasses()
    {
        // DataServices orchestrates storage across providers. It must not reach into
        // the HTTP layer (endpoints), the pipeline layer (workers/activities), or
        // the external API layer (clients).
        var dataServicesAssembly = typeof(RealEstateStar.DataServices.Leads.LeadDataService).Assembly;

        var violations = ImplementationClasses(dataServicesAssembly.GetExportedTypes())
            .Where(t =>
                t.Name.EndsWith("Endpoint") ||
                t.Name.EndsWith("Worker") ||
                t.Name.EndsWith("Activity") ||
                t.Name.EndsWith("Client"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "DataServices is a storage routing layer — it must not contain Endpoint, Worker, Activity, or Client classes. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void DataServices_MustNot_Reference_ClientAssemblies()
    {
        // DataServices must not call external APIs directly. External I/O goes through Clients.*,
        // which are injected via DI (registered in Api and resolved at runtime).
        var dataServicesAssembly = typeof(RealEstateStar.DataServices.Leads.LeadDataService).Assembly;

        var clientRefs = dataServicesAssembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("RealEstateStar.Clients"))
            .Select(a => a.Name!)
            .ToList();

        clientRefs.Should().BeEmpty(
            "DataServices must not reference Clients.* directly — external API calls go through " +
            "injected interfaces defined in Domain, implemented in Clients.*, and wired in Api. " +
            "Violations: {0}", string.Join(", ", clientRefs));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ACTIVITIES LAYER — Pipeline Steps (compute + write one artifact)
    //
    // Allowed:  *Activity (+ *Generator, *Diagnostics as internal helpers)
    // Forbidden: *Endpoint, *Worker, *Client (direct), *Service
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Activities_MustNot_Contain_EndpointOrWorkerOrClientClasses()
    {
        // Activities are self-contained pipeline steps that compute a result and write
        // one artifact. They must not reach into the HTTP layer or call clients directly.
        var violations = GetProductionTypes("RealEstateStar.Activities.")
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t =>
                t.Name.EndsWith("Endpoint") ||
                t.Name.EndsWith("Worker") ||
                t.Name.EndsWith("Client"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Activities are pipeline steps — they must not contain Endpoint, Worker, or Client classes. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Activities_MustNot_Reference_ClientAssemblies()
    {
        // Activities receive injected Domain interfaces. They never depend directly on
        // Clients.* implementations — those are wired by Api.
        var activitiesAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Activities.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .ToList();

        foreach (var assembly in activitiesAssemblies)
        {
            var clientRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.StartsWith("RealEstateStar.Clients"))
                .Select(a => a.Name!)
                .ToList();

            clientRefs.Should().BeEmpty(
                $"{assembly.GetName().Name} must not reference Clients.* directly — external calls go through " +
                "Domain interfaces injected via DI. " +
                "Violations: {0}", string.Join(", ", clientRefs));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SERVICES LAYER — Synchronous Calls (orchestrator awaits these)
    //
    // Allowed:  *Service (+ helpers: *Notifier, *Sender, *Renderer, *Drafter, *Template)
    // Forbidden: *Endpoint, BackgroundService subclasses
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Services_MustNot_Contain_BackgroundServices()
    {
        // Services are synchronous — the orchestrator awaits them inline.
        // If something needs to run in the background, it belongs in Workers.*.
        var backgroundServiceType = typeof(BackgroundService);
        var hostedServiceType = typeof(IHostedService);

        var violations = GetProductionTypes("RealEstateStar.Services.")
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
                backgroundServiceType.IsAssignableFrom(t) ||
                t.GetInterfaces().Any(i => i == hostedServiceType))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Services.* must not contain BackgroundService or IHostedService implementations. " +
            "Background pipeline work belongs in Workers.*. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Services_MustNot_Contain_EndpointClasses()
    {
        // Services handle business logic. HTTP route handlers belong in Api.
        var violations = GetProductionTypes("RealEstateStar.Services.")
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => t.Name.EndsWith("Endpoint"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Services.* must not contain Endpoint classes — HTTP route handlers belong in Api. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // WORKERS LAYER — Channel-Based Background Services
    //
    // Allowed:  *Worker, *Channel, *Scorer, *Orchestrator
    //           (+ support types: *Context, *Source, *Aggregator, *Analyzer, *Provider, *Diagnostics)
    // Forbidden: *Endpoint, direct client references
    //
    // Workers.Shared is the base-class infrastructure (PipelineWorker, WorkerStepBase, etc.)
    // and is excluded from naming enforcement — it contains abstract base types.
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Workers_MustNot_Contain_EndpointClasses()
    {
        // Workers are pipeline processors. HTTP route handling is the responsibility of Api.
        var workerAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Workers.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => a.GetName().Name != "RealEstateStar.Workers.Shared") // base classes only
            .ToList();

        var violations = workerAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => t.Name.EndsWith("Endpoint"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Workers.* must not contain Endpoint classes — HTTP route handlers belong in Api. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Workers_MustNot_Reference_AspNetCore()
    {
        // Workers are pure pipeline processors with no HTTP concerns.
        // ASP.NET Core types (HttpContext, IActionResult, etc.) do not belong here.
        var workerAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Workers.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"));

        foreach (var assembly in workerAssemblies)
        {
            var aspNetRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.StartsWith("Microsoft.AspNetCore"))
                .Select(a => a.Name!)
                .ToList();

            aspNetRefs.Should().BeEmpty(
                $"{assembly.GetName().Name} must not reference Microsoft.AspNetCore.* — " +
                "Workers are pure pipeline processors with no HTTP concerns. " +
                "Violations: {0}", string.Join(", ", aspNetRefs));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CLIENTS LAYER — External API Wrappers
    //
    // Allowed:  *Client, *Sender, *Refresher, *Runner, *Factory
    //           (+ internal helpers: *Diagnostics, *Options, *Policies, *Mappers)
    // Forbidden: *Endpoint, *Worker, *Activity, *DataService
    //
    // NOTE: Clients.Stripe has StripeService and Clients.GoogleOAuth has GoogleOAuthService.
    // These are naming violations (should end with *Client or *Refresher). They are excluded
    // here while the rename is tracked separately.
    // ══════════════════════════════════════════════════════════════════════════

    // TODO: Rename these *Service classes in Clients.* to align with the *Client/*Refresher contract.
    private static readonly HashSet<string> ClientsServiceNamingExclusions = new()
    {
        // StripeService in Clients.Stripe implements IStripeService.
        // Should be renamed to StripeClient (and IStripeService → IStripeClient).
        "RealEstateStar.Clients.Stripe.StripeService",

        // GoogleOAuthService in Clients.GoogleOAuth handles the OAuth2 refresh dance.
        // Should be renamed to GoogleOAuthClient or folded into GoogleOAuthRefresher.
        "RealEstateStar.Clients.GoogleOAuth.GoogleOAuthService",
    };

    [Fact]
    public void Clients_MustNot_Contain_EndpointOrWorkerOrActivityOrDataServiceClasses()
    {
        // Clients wrap external APIs. They are called by Services, Workers, and Activities
        // via injected Domain interfaces. They must not contain HTTP route handlers,
        // pipeline workers, pipeline steps, or storage routing.
        var violations = GetProductionTypes("RealEstateStar.Clients.")
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.IsEnum)
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t => !ClientsServiceNamingExclusions.Contains(t.FullName!))
            .Where(t =>
                t.Name.EndsWith("Endpoint") ||
                t.Name.EndsWith("Worker") ||
                t.Name.EndsWith("Activity") ||
                t.Name.EndsWith("DataService"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Clients.* are external API wrappers — they must not contain Endpoint, Worker, Activity, " +
            "or DataService classes. Violations: {0}", string.Join(", ", violations));
    }

    [Fact]
    public void Clients_MustNot_Reference_DataOrDataServicesAssemblies()
    {
        // Clients call external systems (Anthropic, Stripe, Google, etc.). They must not
        // reach into the storage layer (Data, DataServices) — those are higher-level concerns
        // wired together in Api.
        var clientAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Clients.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .ToList();

        var forbidden = new HashSet<string>
        {
            "RealEstateStar.Data",
            "RealEstateStar.DataServices"
        };

        foreach (var assembly in clientAssemblies)
        {
            var storageRefs = assembly.GetReferencedAssemblies()
                .Where(a => forbidden.Contains(a.Name!))
                .Select(a => a.Name!)
                .ToList();

            storageRefs.Should().BeEmpty(
                $"{assembly.GetName().Name} must not reference Data or DataServices — " +
                "Clients wrap external APIs; storage routing belongs in DataServices. " +
                "Violations: {0}", string.Join(", ", storageRefs));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CROSS-CUTTING: Interface ownership
    //
    // Every public interface must live in Domain. Non-domain assemblies must not
    // define interfaces with the same name as Domain interfaces (would create
    // a parallel contract that cannot be satisfied by the same implementation).
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AllPublicInterfaces_MustBeDefined_InDomain()
    {
        // Domain owns ALL contracts. Any public interface found outside Domain that shadows
        // a Domain interface name creates an ambiguous contract that breaks DI wiring.
        //
        // EXCEPTION: IEndpoint is an Api-internal infrastructure interface used by the REPR
        // pattern dispatcher — it does not represent a domain concept and is intentionally
        // scoped to Api only.
        var apiInternalInterfaces = new HashSet<string> { "IEndpoint", "IStripeService" };

        var domainInterfaces = DomainAssembly.GetExportedTypes()
            .Where(t => t.IsInterface)
            .Select(t => t.Name)
            .ToHashSet();

        var nonDomainAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => a.GetName().Name != "RealEstateStar.Domain")
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"));

        var violations = new List<string>();

        foreach (var assembly in nonDomainAssemblies)
        {
            var shadowedInterfaces = assembly.GetExportedTypes()
                .Where(t => t.IsInterface)
                .Where(t => domainInterfaces.Contains(t.Name))
                .Where(t => !apiInternalInterfaces.Contains(t.Name))
                .Select(t => $"{assembly.GetName().Name} defines {t.FullName}")
                .ToList();

            violations.AddRange(shadowedInterfaces);
        }

        violations.Should().BeEmpty(
            "All public interfaces must be defined in Domain — non-Domain assemblies must not " +
            "duplicate Domain interface names. This creates ambiguous contracts that break DI. " +
            "Violations: {0}", string.Join(", ", violations));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CROSS-CUTTING: No circular dependencies
    //
    // The dependency graph must be a DAG. Cycles indicate architectural violations
    // where two layers have mutual dependencies — a sign of broken boundaries.
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void No_CircularDependencies_BetweenProjects()
    {
        var assemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"))
            .ToDictionary(a => a.GetName().Name!, a => a);

        var graph = assemblies.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetReferencedAssemblies()
                .Where(r => r.Name!.StartsWith("RealEstateStar"))
                .Select(r => r.Name!)
                .ToList());

        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();
        var cycles = new List<string>();

        foreach (var node in graph.Keys)
        {
            DetectCycle(node, graph, visited, inStack, [], cycles);
        }

        cycles.Should().BeEmpty(
            "Circular dependencies between projects indicate broken layer boundaries. " +
            "Use Dependency Inversion (Domain interfaces) to break cycles. " +
            "Cycles: {0}", string.Join("; ", cycles));
    }

    private static void DetectCycle(
        string node,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> path,
        List<string> cycles)
    {
        if (inStack.Contains(node))
        {
            var cycleStart = path.IndexOf(node);
            cycles.Add(string.Join(" → ", path.Skip(cycleStart).Append(node)));
            return;
        }

        if (visited.Contains(node)) return;

        visited.Add(node);
        inStack.Add(node);
        path.Add(node);

        if (graph.TryGetValue(node, out var deps))
        {
            foreach (var dep in deps)
            {
                DetectCycle(dep, graph, visited, inStack, path, cycles);
            }
        }

        path.RemoveAt(path.Count - 1);
        inStack.Remove(node);
    }
}
