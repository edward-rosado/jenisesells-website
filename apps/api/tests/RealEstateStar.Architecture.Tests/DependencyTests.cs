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
    [InlineData("RealEstateStar.Workers.Leads", new[] { "Domain", "Workers.Shared", "Workers.Cma", "Workers.HomeSearch" })]
    [InlineData("RealEstateStar.Workers.Cma", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.HomeSearch", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Workers.WhatsApp", new[] { "Domain", "Workers.Shared" })]
    [InlineData("RealEstateStar.Clients.Anthropic", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Scraper", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.WhatsApp", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.GDrive", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Gmail", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.GoogleOAuth", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Stripe", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Cloudflare", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Turnstile", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Azure", new[] { "Domain" })]
    [InlineData("RealEstateStar.Clients.Gws", new[] { "Domain" })]
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
        var productionAssemblies = Directory.GetFiles(AppContext.BaseDirectory, "RealEstateStar.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => !a.GetName().Name!.Contains("Api"))
            .Where(a => !a.GetName().Name!.Contains("Tests"))
            .Where(a => !a.GetName().Name!.Contains("TestUtilities"));

        foreach (var assembly in productionAssemblies)
        {
            var clientRefs = assembly.GetReferencedAssemblies()
                .Where(a => a.Name!.Contains("Clients"))
                .Select(a => a.Name!)
                .ToList();

            Assert.True(clientRefs.Count == 0,
                $"{assembly.GetName().Name} references {string.Join(", ", clientRefs)} — only Api may reference Clients.*");
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
