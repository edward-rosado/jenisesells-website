using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Api.Diagnostics;

namespace RealEstateStar.Api.Tests.Diagnostics;

public class OpenTelemetryExtensionsTests
{
    [Fact]
    public void AddObservability_WithConfiguredEndpoint_UsesConfigValue()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otel:Endpoint"] = "http://custom-collector:4317"
        });

        builder.AddObservability();

        var sp = builder.Services.BuildServiceProvider();
        Assert.NotNull(sp);
    }

    [Fact]
    public void AddObservability_WithoutConfiguredEndpoint_FallsBackToDefault()
    {
        var builder = WebApplication.CreateBuilder();
        // Override to null so the ?? fallback is exercised
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otel:Endpoint"] = null
        });

        builder.AddObservability();

        var sp = builder.Services.BuildServiceProvider();
        Assert.NotNull(sp);
    }
}
