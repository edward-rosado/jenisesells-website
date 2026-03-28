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

using NetArchTest.Rules;

namespace RealEstateStar.Architecture.Tests;

/// <summary>
/// NetArchTest-based layer dependency rules.
/// These complement DependencyTests.cs (assembly-level) with type-level assertions.
/// </summary>
public class LayerTests
{
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(Domain.Leads.Models.Lead).Assembly;

    private static readonly System.Reflection.Assembly DataServicesAssembly =
        typeof(DataServices.Config.AccountConfigService).Assembly;

    private static readonly System.Reflection.Assembly CmaWorkerAssembly =
        typeof(Workers.Lead.CMA.CmaProcessingWorker).Assembly;

    private static readonly System.Reflection.Assembly HomeSearchWorkerAssembly =
        typeof(Workers.Lead.HomeSearch.HomeSearchProcessingWorker).Assembly;

    [Fact]
    public void Domain_types_should_not_depend_on_DataServices()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.DataServices")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain types depend on DataServices:\n{FormatFailures(result)}");
    }

    [Fact]
    public void Domain_types_should_not_depend_on_any_Worker()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Workers")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain types depend on Workers:\n{FormatFailures(result)}");
    }

    [Fact]
    public void Domain_types_should_not_depend_on_any_Client()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Clients")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain types depend on Clients:\n{FormatFailures(result)}");
    }

    [Fact]
    public void Domain_types_should_not_depend_on_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain types depend on ASP.NET Core:\n{FormatFailures(result)}");
    }

    [Fact]
    public void DataServices_types_should_not_depend_on_any_Worker()
    {
        var result = Types.InAssembly(DataServicesAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Workers")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"DataServices types depend on Workers:\n{FormatFailures(result)}");
    }

    [Fact]
    public void Domain_interfaces_should_live_in_Domain_namespace()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .ResideInNamespaceStartingWith("RealEstateStar.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain interfaces outside Domain namespace:\n{FormatFailures(result)}");
    }

    [Fact]
    public void CmaWorker_types_should_not_depend_on_Data()
    {
        var result = Types.InAssembly(CmaWorkerAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Data")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Workers.Lead.CMA types depend on Data:\n{FormatFailures(result)}" +
            " — CMA is a pure compute worker; storage belongs in Workers.Lead.Orchestrator");
    }

    [Fact]
    public void CmaWorker_types_should_not_depend_on_DataServices()
    {
        var result = Types.InAssembly(CmaWorkerAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.DataServices")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Workers.Lead.CMA types depend on DataServices:\n{FormatFailures(result)}" +
            " — CMA is a pure compute worker; storage orchestration belongs in Workers.Lead.Orchestrator");
    }

    [Fact]
    public void HomeSearchWorker_types_should_not_depend_on_Data()
    {
        var result = Types.InAssembly(HomeSearchWorkerAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Data")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Workers.Lead.HomeSearch types depend on Data:\n{FormatFailures(result)}" +
            " — HomeSearch is a pure compute worker; storage belongs in Workers.Lead.Orchestrator");
    }

    [Fact]
    public void HomeSearchWorker_types_should_not_depend_on_DataServices()
    {
        var result = Types.InAssembly(HomeSearchWorkerAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.DataServices")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Workers.Lead.HomeSearch types depend on DataServices:\n{FormatFailures(result)}" +
            " — HomeSearch is a pure compute worker; storage orchestration belongs in Workers.Lead.Orchestrator");
    }

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? "(no details)"
            : string.Join("\n", result.FailingTypeNames);
}
