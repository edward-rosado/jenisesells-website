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
    [InlineData("RealEstateStar.Data", new[] { "Domain" })]
    [InlineData("RealEstateStar.DataServices", new[] { "Domain" })]
    [InlineData("RealEstateStar.Notifications", new[] { "Domain" })]
    [InlineData("RealEstateStar.Workers.Shared", new[] { "Domain" })]
    [InlineData("RealEstateStar.Workers.Shared.Pdf", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.Leads", new[] { "Domain", "Workers.Shared", "Workers.Cma", "Workers.HomeSearch" })]
    [InlineData("RealEstateStar.Workers.Cma", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.HomeSearch", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.WhatsApp", new[] { "Domain", "Workers.Shared" })]
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

        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Api"))
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
                $"{assemblyName} references {string.Join(", ", clientRefs)} — only Api may reference Clients.* (GoogleOAuth is the only permitted shared infrastructure dep for Google API clients)");
        }
    }

    [Fact]
    public void No_project_outside_Api_references_DataServices()
    {
        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Api"))
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"))
            .Where(a => !a.GetName().Name!.Contains("DataServices"));

        foreach (var assembly in productionAssemblies)
        {
            var dataServiceRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.Contains("DataServices"))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(dataServiceRefs.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", dataServiceRefs)} — only Api may reference DataServices");
        }
    }

    [Fact]
    public void No_project_outside_Api_references_Data()
    {
        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Api"))
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
                $"{assembly.GetName().Name} references {string.Join(", ", dataRefs)} — only Api may reference Data");
        }
    }

    [Fact]
    public void No_project_outside_Api_references_Notifications()
    {
        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Api"))
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"))
            .Where(a => a.GetName().Name != "RealEstateStar.Notifications");

        foreach (var assembly in productionAssemblies)
        {
            var notifRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name == "RealEstateStar.Notifications")
                .Select(a => a.Name!)
                .ToList();

            Assert.True(notifRefs.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", notifRefs)} — only Api may reference Notifications");
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
            "RealEstateStar.Notifications",
            "RealEstateStar.Workers.Shared",
            "RealEstateStar.Workers.Shared.Pdf",
            "RealEstateStar.Workers.Leads",
            "RealEstateStar.Workers.Cma",
            "RealEstateStar.Workers.HomeSearch",
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
    public void Workers_do_not_reference_Data_or_DataServices_or_Notifications()
    {
        var workerAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.Workers.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Tests"));

        var forbidden = new[] { "RealEstateStar.Data", "RealEstateStar.DataServices", "RealEstateStar.Notifications" };

        foreach (var assembly in workerAssemblies)
        {
            var violations = assembly.GetReferencedAssemblies()
                .Where(a => forbidden.Contains(a.Name!))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(violations.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", violations)} — Workers must not reference Data, DataServices, or Notifications");
        }
    }

    [Fact]
    public void CmaWorker_ShouldNotReference_StorageInterfaces()
    {
        // Verify Workers.Cma assembly does not reference IFileStorageProvider or IDocumentStorageProvider.
        // CMA is a pure compute worker — storage is handled upstream by the orchestrator (Workers.Leads).
        var assembly = typeof(Workers.Cma.CmaProcessingWorker).Assembly;
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
            $"Workers.Cma references storage interfaces it must not depend on: {string.Join(", ", violations)}" +
            " — CMA is a pure compute worker; storage belongs in Workers.Leads (the orchestrator)");
    }

    [Fact]
    public void HomeSearchWorker_ShouldNotReference_NotificationInterfaces()
    {
        // Verify Workers.HomeSearch does not reference IAgentNotifier or IHomeSearchNotifier.
        // HomeSearch is a pure compute worker — notifications are dispatched by the orchestrator (Workers.Leads).
        var assembly = typeof(Workers.HomeSearch.HomeSearchProcessingWorker).Assembly;
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
            $"Workers.HomeSearch references notification interfaces it must not depend on: {string.Join(", ", violations)}" +
            " — HomeSearch is a pure compute worker; notifications belong in Workers.Leads (the orchestrator)");
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
            var duplicateInterfaces = assembly.GetExportedTypes()
                .Where(t => t.IsInterface && domainInterfaces.Contains(t.Name))
                .Select(t => $"{assembly.GetName().Name} defines {t.FullName}")
                .ToList();

            violations.AddRange(duplicateInterfaces);
        }

        Assert.True(violations.Count == 0,
            $"Interfaces duplicated outside Domain:\n{string.Join("\n", violations)}");
    }
}
