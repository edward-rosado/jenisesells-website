using System.Reflection;
using FluentAssertions;

namespace RealEstateStar.Architecture.Tests;

/// <summary>
/// Enforces that the Api project remains a thin composition root — it wires dependencies
/// via DI and defines HTTP endpoints, but contains no business logic, no service
/// implementations, and no concrete implementations of Domain interfaces.
///
/// When a test fails, a type that belongs in a lower layer (Workers, Services, Activities,
/// DataServices, or Clients) has drifted into the HTTP boundary layer. To resolve: move the
/// type to the appropriate layer and inject it via the DI container, or add it to the
/// exclusion list below with a TODO comment tracking the cleanup.
/// </summary>
public class ApiCompositionRootTests
{
    private static readonly Assembly ApiAssembly = typeof(RealEstateStar.Api.Program).Assembly;
    private static readonly Assembly DomainAssembly = typeof(RealEstateStar.Domain.Leads.Models.Lead).Assembly;

    // ---------------------------------------------------------------------------
    // Rule 1: Api must not implement Domain interfaces
    // ---------------------------------------------------------------------------

    // TODO: Move these onboarding implementations to a dedicated Workers.Onboarding or
    // Services.Onboarding project. Excluded here to keep CI green during migration.
    private static readonly HashSet<string> DomainInterfaceImplementationExclusions = new()
    {
        // IImageResolver — image resolution for CMA PDF generation.
        // Depends on IWebHostEnvironment (ASP.NET Core infra), which pins it to the Api layer
        // temporarily. Target layer: a new Services.Cma project once IWebHostEnvironment is abstracted.
        "RealEstateStar.Api.Infrastructure.LocalFirstImageResolver",

        // IProcessRunner — shell process execution (Next.js / Wrangler builds).
        // Should move to Services.Onboarding or Workers.Onboarding.
        "RealEstateStar.Api.Features.Onboarding.Tools.ProcessRunner",

        // ISiteDeployService — Cloudflare Pages deployment via wrangler.
        // Should move to Services.Onboarding or Workers.Onboarding.
        "RealEstateStar.Api.Features.Onboarding.Tools.SiteDeployService",

        // IOnboardingTool implementations — individual tool steps in the onboarding chat flow.
        // All should move to Services.Onboarding or Workers.Onboarding.
        "RealEstateStar.Api.Features.Onboarding.Tools.CreateStripeSessionTool",
        "RealEstateStar.Api.Features.Onboarding.Tools.DeploySiteTool",
        "RealEstateStar.Api.Features.Onboarding.Tools.GoogleAuthCardTool",
        "RealEstateStar.Api.Features.Onboarding.Tools.ScrapeUrlTool",
        "RealEstateStar.Api.Features.Onboarding.Tools.SendWhatsAppWelcomeTool",
        "RealEstateStar.Api.Features.Onboarding.Tools.SetBrandingTool",
        "RealEstateStar.Api.Features.Onboarding.Tools.UpdateProfileTool",
    };

    [Fact]
    public void Api_MustNot_ImplementDomainInterfaces()
    {
        var domainInterfaces = DomainAssembly.GetExportedTypes()
            .Where(t => t.IsInterface)
            .ToHashSet();

        // IEndpoint is an Api-internal infrastructure interface, not a Domain interface.
        // All endpoint classes implement it by design — excluded categorically.
        var apiInternalInterfaces = ApiAssembly.GetExportedTypes()
            .Where(t => t.IsInterface)
            .ToHashSet();

        var violations = ApiAssembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => !DomainInterfaceImplementationExclusions.Contains(t.FullName!))
            .Where(t => t.GetInterfaces().Any(i =>
                domainInterfaces.Contains(i) &&
                !apiInternalInterfaces.Contains(i)))
            .Select(t =>
            {
                var implemented = t.GetInterfaces()
                    .Where(i => domainInterfaces.Contains(i) && !apiInternalInterfaces.Contains(i))
                    .Select(i => i.Name);
                return $"{t.FullName} implements {string.Join(", ", implemented)}";
            })
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — it must not implement Domain interfaces. " +
            "Move implementations to Workers, Services, Activities, DataServices, or Clients. " +
            "Violations: {0}", string.Join("; ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 2: Api must not contain BackgroundService implementations
    // ---------------------------------------------------------------------------

    // TODO: Move TrialExpiryService to a dedicated Workers.Onboarding or Workers.Billing project.
    private static readonly HashSet<string> BackgroundServiceExclusions = new()
    {
        // TrialExpiryService — polls for expired trials and triggers Stripe charges.
        // Belongs in a dedicated Workers.Billing or Workers.Onboarding background pipeline.
        "RealEstateStar.Api.Features.Onboarding.Services.TrialExpiryService",
    };

    [Fact]
    public void Api_MustNot_ContainBackgroundServiceImplementations()
    {
        var backgroundServiceType = typeof(BackgroundService);
        var hostedServiceType = typeof(IHostedService);

        var violations = ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => !BackgroundServiceExclusions.Contains(t.FullName!))
            .Where(t =>
                backgroundServiceType.IsAssignableFrom(t) ||
                t.GetInterfaces().Any(i => i == hostedServiceType))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — BackgroundService and IHostedService implementations " +
            "belong in Workers.* projects, not in Api. " +
            "Violations: {0}", string.Join("; ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 3: Api endpoint classes must follow REPR naming (*Endpoint suffix)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Api_EndpointClasses_MustEndWith_Endpoint()
    {
        var iEndpointType = ApiAssembly.GetExportedTypes()
            .FirstOrDefault(t => t.IsInterface && t.Name == "IEndpoint");

        if (iEndpointType is null)
        {
            // IEndpoint not found — skip gracefully (different package structure)
            return;
        }

        var violations = ApiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Contains(iEndpointType))
            .Where(t => !t.Name.EndsWith("Endpoint"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "all classes implementing IEndpoint must end with 'Endpoint' (REPR pattern naming contract). " +
            "Violations: {0}", string.Join("; ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 4: Api must not contain classes ending with *Service (except infrastructure helpers)
    // ---------------------------------------------------------------------------

    // TODO: Migrate these onboarding service classes to dedicated Services.* or Workers.* projects.
    private static readonly HashSet<string> ServiceNamingExclusions = new()
    {
        // GoogleOAuthService — Google OAuth token exchange. Should move to Clients.GoogleOAuth
        // or a new Services.Onboarding project.
        "RealEstateStar.Api.Features.Onboarding.Services.GoogleOAuthService",

        // OnboardingChatService — streaming Claude chat with tool dispatch.
        // Should move to Services.Onboarding or Workers.Onboarding.
        "RealEstateStar.Api.Features.Onboarding.Services.OnboardingChatService",

        // StripeService — Stripe Checkout session management.
        // Should move to Clients.Stripe or Services.Billing.
        "RealEstateStar.Api.Features.Onboarding.Services.StripeService",

        // SiteDeployService — Cloudflare Pages build + deploy via Wrangler.
        // Should move to Services.Onboarding or Workers.Onboarding.
        "RealEstateStar.Api.Features.Onboarding.Tools.SiteDeployService",

        // TrialExpiryService — BackgroundService for trial expiry checks.
        // Should move to Workers.Billing or Workers.Onboarding.
        "RealEstateStar.Api.Features.Onboarding.Services.TrialExpiryService",
    };

    [Fact]
    public void Api_MustNot_ContainClassesEndingWith_Service()
    {
        var violations = ApiAssembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.Name.EndsWith("Extensions"))
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t => !ServiceNamingExclusions.Contains(t.FullName!))
            .Where(t => t.Name.EndsWith("Service"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — classes ending with 'Service' belong in Services.* or Workers.* projects. " +
            "Api should only contain endpoints, middleware, infrastructure wiring, and health checks. " +
            "Violations: {0}", string.Join("; ", violations));
    }

    // ---------------------------------------------------------------------------
    // Rule 5: Api must not contain classes ending with *Store, *Provider, or *DataService
    // ---------------------------------------------------------------------------

    [Fact]
    public void Api_MustNot_ContainStorageImplementations()
    {
        var violations = ApiAssembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsNested)
            .Where(t => !t.Name.EndsWith("Extensions"))
            .Where(t => !IsStaticClass(t))
            .Where(t => !IsRecord(t))
            .Where(t =>
                t.Name.EndsWith("Store") ||
                t.Name.EndsWith("Provider") ||
                t.Name.EndsWith("DataService"))
            .Select(t => t.FullName!)
            .ToList();

        violations.Should().BeEmpty(
            "Api is a composition root — classes ending with 'Store', 'Provider', or 'DataService' " +
            "belong in the Data, DataServices, or Clients layers. " +
            "Violations: {0}", string.Join("; ", violations));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Returns true if the type is a static class (abstract + sealed at the IL level).</summary>
    private static bool IsStaticClass(Type t) => t.IsAbstract && t.IsSealed;

    /// <summary>Returns true if the type is a C# record (compiler-generated clone method).</summary>
    private static bool IsRecord(Type t) => t.GetMethod("<Clone>$") is not null;
}
