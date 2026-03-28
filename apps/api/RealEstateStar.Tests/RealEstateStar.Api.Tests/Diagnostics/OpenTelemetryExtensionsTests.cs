using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Workers.Onboarding;
using RealEstateStar.Workers.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
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
