// ╔══════════════════════════════════════════════════════════════════════╗
// ║  ARCHITECTURE GUARD — DO NOT MODIFY WITHOUT EXPLICIT USER APPROVAL  ║
// ║                                                                      ║
// ║  These tests enforce the project's dependency and naming rules.       ║
// ║  AI agents: you MUST NOT add exclusions, weaken rules, or modify     ║
// ║  these tests to make your code compile. If your code violates an     ║
// ║  architecture rule, fix YOUR code — not the test.                    ║
// ║                                                                      ║
// ║  Changing these tests requires the commit message to contain:         ║
// ║  [arch-change-approved] — CI will reject without it.                 ║
// ╚══════════════════════════════════════════════════════════════════════╝

using FluentAssertions;
using System.Reflection;

namespace RealEstateStar.Architecture.Tests;

public class DependencyTests
{
    [Fact]
    public void Domain_has_no_project_dependencies()
    {
        var assembly = typeof(Domain.Leads.Models.Lead).Assembly;
        var violations = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("RealEstateStar"))
            .Select(a => a.Name!)
            .ToList();

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("RealEstateStar.Data", new[] { "Domain", "Clients" })]
    [InlineData("RealEstateStar.DataServices", new[] { "Domain", "Data" })]
    [InlineData("RealEstateStar.Workers.Shared", new[] { "Domain" })]
    [InlineData("RealEstateStar.Activities.Pdf", new[] { "Domain", "DataServices", "Workers.Shared" })]
    [InlineData("RealEstateStar.Activities.Lead.Persist", new[] { "Domain" })]
    [InlineData("RealEstateStar.Services.AgentNotifier", new[] { "Domain", "DataServices", "Clients", "Workers.Shared" })]
    [InlineData("RealEstateStar.Services.LeadCommunicator", new[] { "Domain", "DataServices", "Clients", "Workers.Shared" })]
    [InlineData("RealEstateStar.Services.AgentConfig", new[] { "Domain" })]
    [InlineData("RealEstateStar.Services.BrandMerge", new[] { "Domain" })]
    [InlineData("RealEstateStar.Services.WelcomeNotification", new[] { "Domain" })]
    [InlineData("RealEstateStar.Activities.Lead.ContactDetection", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Activities.Activation.PersistAgentProfile", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Activities.Activation.BrandMerge", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Activities.Activation.ContactImportPersist", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Lead.Orchestrator", new[] { "Domain", "Workers.Shared", "Activities.Pdf", "Activities.Lead.Persist", "Services.AgentNotifier", "Services.LeadCommunicator", "Workers.Lead.CMA", "Workers.Lead.HomeSearch" })]
    [InlineData("RealEstateStar.Workers.Lead.CMA", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Lead.HomeSearch", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.WhatsApp", new[] { "Domain", "Workers.Shared" })]
    // Activation workers — each is a pure compute worker with Domain + Workers.Shared only
    [InlineData("RealEstateStar.Workers.Activation.AgentDiscovery", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.BrandExtraction", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.BrandingDiscovery", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.BrandVoice", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.CmaStyle", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.Coaching", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.ComplianceAnalysis", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.DriveIndex", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.EmailFetch", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.FeeStructure", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.MarketingStyle", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.Personality", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.PipelineAnalysis", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.VoiceExtraction", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Activation.WebsiteStyle", new[] { "Domain", "Workers.Shared" })]
    // Activation orchestrator — coordinates all activation workers + activities
    [InlineData("RealEstateStar.Workers.Activation.Orchestrator", new[] {
        "Domain", "Workers.Shared",
        "Workers.Activation.AgentDiscovery", "Workers.Activation.BrandExtraction",
        "Workers.Activation.BrandingDiscovery", "Workers.Activation.BrandVoice",
        "Workers.Activation.CmaStyle", "Workers.Activation.Coaching",
        "Workers.Activation.ComplianceAnalysis", "Workers.Activation.DriveIndex",
        "Workers.Activation.EmailFetch", "Workers.Activation.FeeStructure",
        "Workers.Activation.MarketingStyle", "Workers.Activation.Personality",
        "Workers.Activation.PipelineAnalysis", "Workers.Activation.VoiceExtraction",
        "Workers.Activation.WebsiteStyle",
        "Activities.Activation.PersistAgentProfile", "Activities.Activation.BrandMerge",
        "Activities.Lead.ContactDetection", "Activities.Activation.ContactImportPersist",
    })]
    [InlineData("RealEstateStar.Clients.Anthropic", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Scraper", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.WhatsApp", new[] { "Domain" })]
    // GoogleOAuth is the shared Google-credential infrastructure layer for Google API clients —
    // the same relationship that Workers.Shared has to Workers.*. Each Google client depends on
    // GoogleOAuth; no other cross-client dependency is permitted (enforced by
    // Clients_do_not_reference_other_Clients below, which explicitly exempts GoogleOAuth).
    [InlineData("RealEstateStar.Clients.GDrive", new[] { "Domain", "Clients.GoogleOAuth" })]
    [InlineData("RealEstateStar.Clients.Gmail", new[] { "Domain", "Clients.GoogleOAuth" })]
    [InlineData("RealEstateStar.Clients.GDocs", new[] { "Domain", "Clients.GoogleOAuth" })]
    [InlineData("RealEstateStar.Clients.GSheets", new[] { "Domain", "Clients.GoogleOAuth" })]
    [InlineData("RealEstateStar.Clients.GoogleOAuth", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Stripe", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Cloudflare", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Turnstile", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Azure", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Gws", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.RentCast", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Zillow", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.GooglePlaces", new[] { "Domain" })]
    public void Project_only_depends_on_allowed_projects(string projectName, string[] allowedSuffixes)
    {
        var assembly = Assembly.Load(projectName);
        var allowed = allowedSuffixes
            .Select(s => $"RealEstateStar.{s}")
            .Append(projectName)
            .ToHashSet();

        var violations = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("RealEstateStar"))
            .Where(a => !allowed.Contains(a.Name!))
            .Select(a => a.Name!)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Api_is_the_only_project_that_references_Clients()
    {
        // GoogleOAuth is the shared Google-credential infrastructure layer — the same relationship
        // that Workers.Shared has to Workers.*. Gmail, GDrive, GDocs, and GSheets may reference
        // GoogleOAuth (for GoogleCredentialFactory); no other non-Api project may reference any
        // Clients.* assembly.
        const string sharedGoogleInfra = "RealEstateStar.Clients.GoogleOAuth";
        var googleApiClients = new HashSet<string>
        {
            "RealEstateStar.Clients.Gmail",
            "RealEstateStar.Clients.GDrive",
            "RealEstateStar.Clients.GDocs",
            "RealEstateStar.Clients.GSheets"
        };

        // Composition roots (Api and Functions) are allowed to reference Clients.*
        var compositionRoots = new HashSet<string> { "Api", "Functions" };

        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !compositionRoots.Any(k => a.GetName().Name!.Contains(k)))
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"));

        foreach (var assembly in productionAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            var clientRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.Contains("Clients"))
                // Google API clients are allowed to reference the shared GoogleOAuth infrastructure
                .Where(a => !(googleApiClients.Contains(assemblyName) && a.Name == sharedGoogleInfra))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(clientRefs.Count == 0,
                $"{assemblyName} references {string.Join(", ", clientRefs)} — only Api and Functions may reference Clients.* (GoogleOAuth is the only permitted shared infrastructure dep for Google API clients)");
        }
    }

    [Fact]
    public void Only_Api_Services_Activities_may_reference_DataServices()
    {
        // Services persist failure/fallback records via DataServices.
        // Activities persist pipeline artifacts via DataServices.
        // Functions is a second composition root alongside Api — it wires DI and may reference DataServices.
        // Everyone else goes through Domain interfaces only.
        var allowed = new HashSet<string>
        {
            "Api", "Services", "Activities", "DataServices", "Tests", "TestUtilities", "Functions"
        };

        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !allowed.Any(k => a.GetName().Name!.Contains(k)));

        foreach (var assembly in productionAssemblies)
        {
            var dataServiceRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.Contains("DataServices"))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(dataServiceRefs.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", dataServiceRefs)} — only Api, Services, and Activities may reference DataServices");
        }
    }

    [Fact]
    public void No_project_outside_Api_references_Data()
    {
        // Functions is a second composition root alongside Api — both are allowed to reference Data.
        var compositionRoots = new HashSet<string> { "Api", "Functions" };

        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !compositionRoots.Any(r => a.GetName().Name!.Contains(r)))
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"))
            .Where(a => a.GetName().Name != "RealEstateStar.Data");

        foreach (var assembly in productionAssemblies)
        {
            var dataRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name == "RealEstateStar.Data")
                .Select(a => a.Name!)
                .ToList();

            Assert.True(dataRefs.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", dataRefs)} — only composition roots (Api, Functions) may reference Data");
        }
    }

    [Fact]
    public void Api_only_depends_on_allowed_projects()
    {
        var assembly = Assembly.Load("RealEstateStar.Api");
        var allowed = new HashSet<string>
        {
            "RealEstateStar.Api",
            "RealEstateStar.Domain",
            "RealEstateStar.Data",
            "RealEstateStar.DataServices",
            "RealEstateStar.Workers.Shared",
            "RealEstateStar.Activities.Pdf",
            "RealEstateStar.Activities.Lead.Persist",
            "RealEstateStar.Activities.Activation.PersistAgentProfile",
            "RealEstateStar.Activities.Activation.BrandMerge",
            "RealEstateStar.Activities.Lead.ContactDetection",
            "RealEstateStar.Activities.Activation.ContactImportPersist",
            "RealEstateStar.Services.AgentNotifier",
            "RealEstateStar.Services.LeadCommunicator",
            "RealEstateStar.Services.AgentConfig",
            "RealEstateStar.Services.BrandMerge",
            "RealEstateStar.Services.WelcomeNotification",
            "RealEstateStar.Workers.Lead.Orchestrator",
            "RealEstateStar.Workers.Lead.CMA",
            "RealEstateStar.Workers.Lead.HomeSearch",
            "RealEstateStar.Workers.WhatsApp",
            // Activation pipeline: Api references the orchestrator directly to register it as a HostedService
            "RealEstateStar.Workers.Activation.Orchestrator",
            "RealEstateStar.Clients.Anthropic",
            "RealEstateStar.Clients.Scraper",
            "RealEstateStar.Clients.WhatsApp",
            "RealEstateStar.Clients.GDrive",
            "RealEstateStar.Clients.Gmail",
            "RealEstateStar.Clients.GDocs",
            "RealEstateStar.Clients.GSheets",
            "RealEstateStar.Clients.GoogleOAuth",
            "RealEstateStar.Clients.Stripe",
            "RealEstateStar.Clients.Cloudflare",
            "RealEstateStar.Clients.Turnstile",
            "RealEstateStar.Clients.Azure",
            "RealEstateStar.Clients.Gws",
            "RealEstateStar.Clients.RentCast",
            "RealEstateStar.Clients.Zillow",
            "RealEstateStar.Clients.GooglePlaces",
        };

        var violations = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("RealEstateStar"))
            .Where(a => !allowed.Contains(a.Name!))
            .Select(a => a.Name!)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Functions_only_depends_on_allowed_projects()
    {
        // Functions is a second composition root alongside Api.
        // Phase 0: minimal refs — Domain, DataServices, Data, Workers.Shared.
        // Phase 1: WhatsApp webhook + retry — adds Workers.WhatsApp.
        // More refs will be added in Phases 2-3 as functions are built.
        //
        // Note: Functions is referenced with ReferenceOutputAssembly=false to avoid a Program class
        // name collision with Api. The DLL is loaded from its build output path directly.
        var functionsPath = Path.Combine(AppContext.BaseDirectory, "RealEstateStar.Functions.dll");
        Assert.True(File.Exists(functionsPath),
            $"RealEstateStar.Functions.dll not found at {functionsPath} — ensure Functions project is built");

        var assembly = Assembly.LoadFrom(functionsPath);
        var allowed = new HashSet<string>
        {
            "RealEstateStar.Functions",
            // Core infrastructure
            "RealEstateStar.Domain",
            "RealEstateStar.DataServices",
            "RealEstateStar.Data",
            "RealEstateStar.Workers.Shared",
            // Phase 1: WhatsApp — owns IConversationHandler impl, WhatsAppRetryJob
            "RealEstateStar.Workers.WhatsApp",
            // Phase 1: Activation workers (pure compute, Domain + Workers.Shared only)
            "RealEstateStar.Workers.Activation.AgentDiscovery",
            "RealEstateStar.Workers.Activation.BrandExtraction",
            "RealEstateStar.Workers.Activation.BrandingDiscovery",
            "RealEstateStar.Workers.Activation.BrandVoice",
            "RealEstateStar.Workers.Activation.CmaStyle",
            "RealEstateStar.Workers.Activation.Coaching",
            "RealEstateStar.Workers.Activation.ComplianceAnalysis",
            "RealEstateStar.Workers.Activation.DriveIndex",
            "RealEstateStar.Workers.Activation.EmailFetch",
            "RealEstateStar.Workers.Activation.FeeStructure",
            "RealEstateStar.Workers.Activation.MarketingStyle",
            "RealEstateStar.Workers.Activation.Orchestrator",
            "RealEstateStar.Workers.Activation.Personality",
            "RealEstateStar.Workers.Activation.PipelineAnalysis",
            "RealEstateStar.Workers.Activation.VoiceExtraction",
            "RealEstateStar.Workers.Activation.WebsiteStyle",
            // Phase 1: Activation activities
            "RealEstateStar.Activities.Activation.BrandMerge",
            "RealEstateStar.Activities.Activation.PersistAgentProfile",
            "RealEstateStar.Activities.Activation.ContactImportPersist",
            // Phase 1: Lead activities and workers
            "RealEstateStar.Activities.Lead.ContactDetection",
            "RealEstateStar.Activities.Lead.Persist",
            "RealEstateStar.Activities.Pdf",
            "RealEstateStar.Workers.Lead.CMA",
            "RealEstateStar.Workers.Lead.HomeSearch",
            "RealEstateStar.Workers.Lead.Orchestrator",
            // Phase 1: Services (composition root may reference Services)
            "RealEstateStar.Services.AgentNotifier",
            "RealEstateStar.Services.LeadCommunicator",
            "RealEstateStar.Services.AgentConfig",
            "RealEstateStar.Services.BrandMerge",
            "RealEstateStar.Services.WelcomeNotification",
            // Phase 2: Clients.Azure — Functions is a composition root and may reference Clients.*
            // to register IDistributedContentCache, IIdempotencyStore, ITokenStore.
            // [arch-change-approved]
            "RealEstateStar.Clients.Azure",
            // Phase 3: Additional clients — Lead + Activation pipeline DI wiring requires these.
            // Functions is a second composition root alongside Api; both may reference Clients.*.
            // [arch-change-approved]
            "RealEstateStar.Clients.Anthropic",
            "RealEstateStar.Clients.Gmail",
            "RealEstateStar.Clients.GDrive",
            "RealEstateStar.Clients.GDocs",
            "RealEstateStar.Clients.GSheets",
            "RealEstateStar.Clients.GoogleOAuth",
            "RealEstateStar.Clients.Scraper",
            "RealEstateStar.Clients.RentCast",
            // Clients.Zillow — Zillow Reviews API (Bridge Interactive) for activation discovery
            // [arch-change-approved]
            "RealEstateStar.Clients.Zillow",
            "RealEstateStar.Clients.GooglePlaces",
            // Clients.Gws — GWS CLI wrapper used by activation pipeline
            // [arch-change-approved]
            "RealEstateStar.Clients.Gws",
        };

        var violations = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("RealEstateStar"))
            .Where(a => !allowed.Contains(a.Name!))
            .Select(a => a.Name!)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Workers_do_not_reference_AspNetCore()
    {
        var workerAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Workers.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"));

        foreach (var assembly in workerAssemblies)
        {
            var aspNetRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.StartsWith("Microsoft.AspNetCore"))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(aspNetRefs.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", aspNetRefs)} — Workers must not depend on ASP.NET Core");
        }
    }

    /// <summary>
    /// Azure Linux Consumption plan does NOT include Microsoft.AspNetCore.App shared framework.
    /// If the Functions project references it, the worker process crashes on every invocation
    /// with a silent 500 (empty body, no error in App Insights). This guard prevents regression.
    /// </summary>
    [Fact]
    public void Functions_must_not_reference_AspNetCore()
    {
        // Check the Functions assembly for ASP.NET Core references
        var functionsAssembly = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Functions.dll")
            .Select(Assembly.LoadFrom)
            .FirstOrDefault();

        // Functions assembly may not be in test output — check csproj instead
        if (functionsAssembly != null)
        {
            // DataProtection is required for DPAPI token encryption (IDataProtectionProvider).
            // It's a standalone NuGet package, not part of the ASP.NET Core shared framework,
            // so it's safe to deploy on Azure Linux Consumption plan.
            var dataProtectionExcluded = new HashSet<string>
            {
                "Microsoft.AspNetCore.DataProtection",
                "Microsoft.AspNetCore.DataProtection.Abstractions",
            };

            var aspNetRefs = functionsAssembly.GetReferencedAssemblies()
                .Where(a => a.Name!.StartsWith("Microsoft.AspNetCore"))
                .Where(a => !dataProtectionExcluded.Contains(a.Name!))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(aspNetRefs.Count == 0,
                $"RealEstateStar.Functions references {string.Join(", ", aspNetRefs)} — " +
                "Azure Linux Consumption plan does NOT include Microsoft.AspNetCore.App. " +
                "Use HttpRequestData/HttpResponseData instead of HttpRequest/IActionResult.");
        }

        // Also check the csproj for FrameworkReference (belt + suspenders)
        var csprojPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "RealEstateStar.Functions", "RealEstateStar.Functions.csproj");
        if (File.Exists(csprojPath))
        {
            var csprojContent = File.ReadAllText(csprojPath);
            Assert.DoesNotContain("Microsoft.AspNetCore.App", csprojContent,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Extensions.Http.AspNetCore", csprojContent,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void No_circular_dependencies_between_projects()
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

        Assert.True(cycles.Count == 0,
            $"Circular dependencies detected:\n{string.Join("\n", cycles)}");
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

    [Fact]
    public void Clients_do_not_reference_other_Clients()
    {
        // GoogleOAuth is the shared Google-credential infrastructure layer — the same relationship
        // that Workers.Shared has to Workers.*. Gmail, GDrive, GDocs, and GSheets may reference
        // GoogleOAuth (for GoogleCredentialFactory); no other cross-client dependency is allowed.
        const string sharedGoogleInfra = "RealEstateStar.Clients.GoogleOAuth";
        var googleApiClients = new HashSet<string>
        {
            "RealEstateStar.Clients.Gmail",
            "RealEstateStar.Clients.GDrive",
            "RealEstateStar.Clients.GDocs",
            "RealEstateStar.Clients.GSheets"
        };

        var clientAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Clients.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"));

        foreach (var assembly in clientAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            var crossClientRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.StartsWith("RealEstateStar.Clients."))
                .Where(a => a.Name != assemblyName)
                // Google API clients are allowed to reference the shared GoogleOAuth infrastructure
                .Where(a => !(googleApiClients.Contains(assemblyName) && a.Name == sharedGoogleInfra))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(crossClientRefs.Count == 0,
                $"{assemblyName} references {string.Join(", ", crossClientRefs)} — Clients must not reference other Clients (GoogleOAuth is the only permitted shared infrastructure dep for Google API clients)");
        }
    }

    [Fact]
    public void Workers_do_not_reference_Data_or_DataServices()
    {
        var workerAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Workers.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"));

        var forbidden = new[] { "RealEstateStar.Data", "RealEstateStar.DataServices" };

        foreach (var assembly in workerAssemblies)
        {
            var violations = assembly.GetReferencedAssemblies()
                .Where(a => forbidden.Contains(a.Name!))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(violations.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", violations)} — Workers must not reference Data or DataServices");
        }
    }

    [Fact]
    public void CmaWorker_ShouldNotReference_StorageInterfaces()
    {
        // Verify Workers.Lead.CMA assembly does not reference IFileStorageProvider or IDocumentStorageProvider.
        // CMA is a pure compute worker — storage is handled upstream by the orchestrator (Durable Functions in Phase 4+).
        // Phase 4: CmaProcessingWorker (BackgroundService) removed; use RentCastCompSource as assembly anchor.
        var assembly = typeof(Workers.Lead.CMA.RentCastCompSource).Assembly;
        var forbiddenTypeNames = new HashSet<string>
        {
            "IFileStorageProvider",
            "IDocumentStorageProvider"
        };

        var violations = assembly.GetTypes()
            .SelectMany(t => t.GetMembers(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static))
            .OfType<System.Reflection.MethodBase>()
            .SelectMany(m =>
            {
                try { return m.GetParameters().Select(p => p.ParameterType); }
                catch { return []; }
            })
            .Concat(assembly.GetTypes()
                .SelectMany(t =>
                {
                    try
                    {
                        return t.GetConstructors(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance)
                            .SelectMany(c => c.GetParameters().Select(p => p.ParameterType));
                    }
                    catch { return []; }
                }))
            .Where(t => forbiddenTypeNames.Contains(t.Name))
            .Select(t => t.Name)
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0,
            $"Workers.Lead.CMA references storage interfaces it must not depend on: {string.Join(", ", violations)}" +
            " — CMA is a pure compute worker; storage belongs in Workers.Lead.Orchestrator (the orchestrator)");
    }

    [Fact]
    public void HomeSearchWorker_ShouldNotReference_NotificationInterfaces()
    {
        // Verify Workers.Lead.HomeSearch does not reference IAgentNotifier or IHomeSearchNotifier.
        // HomeSearch is a pure compute worker — notifications are dispatched by the orchestrator (Durable Functions in Phase 4+).
        // Phase 4: HomeSearchProcessingWorker (BackgroundService) removed; use ScraperHomeSearchProvider as assembly anchor.
        var assembly = typeof(Workers.Lead.HomeSearch.ScraperHomeSearchProvider).Assembly;
        var forbiddenTypeNames = new HashSet<string>
        {
            "IAgentNotifier",
            "IEmailNotifier",
            "IHomeSearchNotifier"
        };

        var violations = assembly.GetTypes()
            .SelectMany(t =>
            {
                try
                {
                    return t.GetConstructors(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)
                        .SelectMany(c => c.GetParameters().Select(p => p.ParameterType));
                }
                catch { return []; }
            })
            .Concat(assembly.GetTypes()
                .SelectMany(t => t.GetMembers(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static))
                .OfType<System.Reflection.MethodBase>()
                .SelectMany(m =>
                {
                    try { return m.GetParameters().Select(p => p.ParameterType); }
                    catch { return []; }
                }))
            .Where(t => forbiddenTypeNames.Contains(t.Name))
            .Select(t => t.Name)
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0,
            $"Workers.Lead.HomeSearch references notification interfaces it must not depend on: {string.Join(", ", violations)}" +
            " — HomeSearch is a pure compute worker; notifications belong in Workers.Lead.Orchestrator (the orchestrator)");
    }

    // ── Exclusion count guard — adding an exclusion without updating this count fails CI ──

    [Fact]
    public void ExclusionCounts_MustMatchExpected()
    {
        // If you need to add an exclusion, you MUST update this count too.
        // This prevents AI agents from silently expanding exclusion lists.
        // Current counts verified on 2026-03-28.

        // googleApiClients — the only Clients allowed to reference another Client (GoogleOAuth).
        // Adding a new Google API client here requires user approval.
        var googleApiClientsInClientRefTest = new HashSet<string>
        {
            "RealEstateStar.Clients.Gmail",
            "RealEstateStar.Clients.GDrive",
            "RealEstateStar.Clients.GDocs",
            "RealEstateStar.Clients.GSheets"
        };
        googleApiClientsInClientRefTest.Count.Should().Be(4,
            "googleApiClients exclusion set changed — was an exclusion added without approval?");

        // Keywords that exempt an assembly from the DataServices reference check.
        // This set controls which projects are allowed to depend on DataServices.
        // "Functions" added 2026-04-02: Functions is a second composition root alongside Api.
        var dataServicesAllowedKeywords = new HashSet<string>
        {
            "Api", "Services", "Activities", "DataServices", "Tests", "TestUtilities", "Functions"
        };
        dataServicesAllowedKeywords.Count.Should().Be(7,
            "DataServices allowed-caller keyword set changed — was a new caller exempted without approval?");

        // Api allowed-project allowlist — all projects Api may directly reference.
        // Adding a project here expands the composition root's surface area.
        var apiAllowedProjects = new HashSet<string>
        {
            "RealEstateStar.Api",
            "RealEstateStar.Domain",
            "RealEstateStar.Data",
            "RealEstateStar.DataServices",
            "RealEstateStar.Workers.Shared",
            "RealEstateStar.Activities.Pdf",
            "RealEstateStar.Activities.Lead.Persist",
            "RealEstateStar.Activities.Activation.PersistAgentProfile",
            "RealEstateStar.Activities.Activation.BrandMerge",
            "RealEstateStar.Activities.Lead.ContactDetection",
            "RealEstateStar.Activities.Activation.ContactImportPersist",
            "RealEstateStar.Services.AgentNotifier",
            "RealEstateStar.Services.LeadCommunicator",
            "RealEstateStar.Services.AgentConfig",
            "RealEstateStar.Services.BrandMerge",
            "RealEstateStar.Services.WelcomeNotification",
            "RealEstateStar.Workers.Lead.Orchestrator",
            "RealEstateStar.Workers.Lead.CMA",
            "RealEstateStar.Workers.Lead.HomeSearch",
            "RealEstateStar.Workers.WhatsApp",
            "RealEstateStar.Clients.Anthropic",
            "RealEstateStar.Clients.Scraper",
            "RealEstateStar.Clients.WhatsApp",
            "RealEstateStar.Clients.GDrive",
            "RealEstateStar.Clients.Gmail",
            "RealEstateStar.Clients.GDocs",
            "RealEstateStar.Clients.GSheets",
            "RealEstateStar.Clients.GoogleOAuth",
            "RealEstateStar.Clients.Stripe",
            "RealEstateStar.Clients.Cloudflare",
            "RealEstateStar.Clients.Turnstile",
            "RealEstateStar.Clients.Azure",
            "RealEstateStar.Clients.Gws",
            "RealEstateStar.Clients.RentCast",
            "RealEstateStar.Clients.Zillow",
        };
        apiAllowedProjects.Count.Should().Be(35,
            "Api allowed-project count changed — was a new dependency added to the composition root without approval?");
    }

    [Fact]
    public void All_Domain_exported_types_are_public()
    {
        var domainAssembly = typeof(Domain.Leads.Models.Lead).Assembly;
        var nonPublicExports = domainAssembly.GetTypes()
            .Where(t => t.IsPublic || t.IsNestedPublic ? false : t.Namespace?.StartsWith("RealEstateStar.Domain") == true)
            .Where(t => !t.Name.StartsWith("<")) // skip compiler-generated
            .Select(t => t.FullName!)
            .ToList();

        // Internal types are acceptable for implementation details,
        // but exported types used by other projects must be public.
        // This test catches accidentally-internal domain models.
        Assert.True(nonPublicExports.Count == 0,
            $"Domain types that should be public:\n{string.Join("\n", nonPublicExports)}");
    }

    [Fact]
    public void Domain_interfaces_are_only_defined_in_Domain()
    {
        var domainAssembly = typeof(Domain.Leads.Models.Lead).Assembly;
        var domainInterfaces = domainAssembly.GetExportedTypes()
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
            Type[] types;
            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (Exception)
            {
                // Skip assemblies whose dependencies are not present in the test output dir.
                // RealEstateStar.Functions depends on the Azure Functions Worker SDK DLLs which
                // are not copied here. Since Functions is a composition root (no new interfaces),
                // skipping it does not weaken this test's intent.
                continue;
            }

            var duplicateInterfaces = types
                .Where(t => t.IsInterface && domainInterfaces.Contains(t.Name))
                .Select(t => $"{assembly.GetName().Name} defines {t.FullName}")
                .ToList();

            violations.AddRange(duplicateInterfaces);
        }

        Assert.True(violations.Count == 0,
            $"Interfaces duplicated outside Domain:\n{string.Join("\n", violations)}");
    }

    // ── Observability guards ─────────────────────────────────────────────────

    /// <summary>
    /// Both composition roots (Api and Functions) must export logs to OTLP via Serilog.
    /// Without this, logs on Azure Consumption plan are lost (console only).
    /// This test scans source files for the Serilog OTLP sink configuration.
    /// </summary>
    [Theory]
    [InlineData("RealEstateStar.Api", "Logging/LoggingExtensions.cs")]
    [InlineData("RealEstateStar.Functions", "Program.cs")]
    public void CompositionRoot_exports_logs_to_OTLP(string project, string sourceFile)
    {
        var csprojPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            project, $"{project}.csproj");

        // Verify the Serilog OTLP sink package is referenced
        if (File.Exists(csprojPath))
        {
            var csproj = File.ReadAllText(csprojPath);
            Assert.True(
                csproj.Contains("Serilog.Sinks.OpenTelemetry"),
                $"{project}.csproj must reference Serilog.Sinks.OpenTelemetry for log export");
        }

        // Verify the source file configures the OTLP sink with auth headers
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            project, sourceFile);
        if (File.Exists(sourcePath))
        {
            var source = File.ReadAllText(sourcePath);
            Assert.True(
                source.Contains("OpenTelemetry") && source.Contains("Otel:Endpoint"),
                $"{project}/{sourceFile} must configure Serilog OTLP sink with Otel:Endpoint — " +
                "without log export, Azure Consumption plan logs are lost");

            // Auth headers are REQUIRED — Grafana Cloud rejects unauthenticated OTLP pushes.
            // Without this, logs silently disappear (endpoint gets 401, Serilog swallows it).
            Assert.True(
                source.Contains("Otel:Headers") && source.Contains("opts.Headers"),
                $"{project}/{sourceFile} must pass Otel:Headers to the Serilog OTLP sink — " +
                "Grafana Cloud requires Authorization header for log ingestion");

            // HttpProtobuf is REQUIRED — Grafana Cloud OTLP gateway only accepts HTTP/protobuf.
            // The Serilog sink defaults to gRPC which silently fails against the OTLP gateway.
            Assert.True(
                source.Contains("HttpProtobuf"),
                $"{project}/{sourceFile} must set OtlpProtocol.HttpProtobuf — " +
                "Grafana Cloud OTLP gateway does not support gRPC");
        }
    }
}
