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

    private static readonly System.Reflection.Assembly NotificationsAssembly =
        typeof(Notifications.Leads.CascadingAgentNotifier).Assembly;

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
    public void Domain_types_should_not_depend_on_Notifications()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Notifications")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain types depend on Notifications:\n{FormatFailures(result)}");
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
    public void DataServices_types_should_not_depend_on_Notifications()
    {
        var result = Types.InAssembly(DataServicesAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Notifications")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"DataServices types depend on Notifications:\n{FormatFailures(result)}");
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
    public void Notifications_types_should_not_depend_on_DataServices()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.DataServices")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Notifications types depend on DataServices:\n{FormatFailures(result)}");
    }

    [Fact]
    public void Notifications_types_should_not_depend_on_any_Worker()
    {
        var result = Types.InAssembly(NotificationsAssembly)
            .ShouldNot()
            .HaveDependencyOn("RealEstateStar.Workers")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Notifications types depend on Workers:\n{FormatFailures(result)}");
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

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? "(no details)"
            : string.Join("\n", result.FailingTypeNames);
}
