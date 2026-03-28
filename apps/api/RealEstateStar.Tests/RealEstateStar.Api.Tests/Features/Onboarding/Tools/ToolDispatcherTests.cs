using Xunit;
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
using RealEstateStar.Notifications.WhatsApp;
using System.Text.Json;
using Moq;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class ToolDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_RoutesToCorrectTool()
    {
        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tool executed");

        var dispatcher = new ToolDispatcher([mockTool.Object], Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolDispatcher>.Instance);
        var session = OnboardingSession.Create(null);

        var result = await dispatcher.DispatchAsync("test_tool", default, session, CancellationToken.None);

        Assert.Equal("tool executed", result);
    }

    [Fact]
    public async Task DispatchAsync_UnknownTool_Throws()
    {
        var dispatcher = new ToolDispatcher([], Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolDispatcher>.Instance);
        var session = OnboardingSession.Create(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync("nonexistent", default, session, CancellationToken.None));
    }

    [Fact]
    public void HasTool_ReturnsTrueForRegisteredTool()
    {
        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("my_tool");
        var dispatcher = new ToolDispatcher([mockTool.Object], Microsoft.Extensions.Logging.Abstractions.NullLogger<ToolDispatcher>.Instance);

        Assert.True(dispatcher.HasTool("my_tool"));
        Assert.False(dispatcher.HasTool("other_tool"));
    }

    [Fact]
    public async Task UpdateProfileTool_UpdatesSessionProfile()
    {
        var tool = new UpdateProfileTool(new OnboardingStateMachine());
        var session = OnboardingSession.Create(null);
        var json = JsonSerializer.Deserialize<JsonElement>("""{"name":"Jane Doe","brokerage":"RE/MAX"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Equal("Jane Doe", session.Profile!.Name);
        Assert.Equal("RE/MAX", session.Profile.Brokerage);
        Assert.Contains("Jane Doe", result);
    }

    [Fact]
    public async Task SetBrandingTool_SetsBrandingColors()
    {
        var tool = new SetBrandingTool();
        var session = OnboardingSession.Create(null);
        var json = JsonSerializer.Deserialize<JsonElement>("""{"primaryColor":"#ff0000","accentColor":"#00ff00"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Equal("#ff0000", session.Profile!.PrimaryColor);
        Assert.Equal("#00ff00", session.Profile.AccentColor);
        Assert.Contains("#ff0000", result);
    }

    [Fact]
    public async Task DeploySiteTool_SetsSiteUrl()
    {
        var deploySvc = new Mock<ISiteDeployService>();
        deploySvc.Setup(d => d.DeployAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://jane-doe.realestatestar.com");
        var sm = new OnboardingStateMachine();
        var tool = new DeploySiteTool(deploySvc.Object, sm, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploySiteTool>.Instance);
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Jane Doe" };
        // Advance to GenerateSite so deploy_site can advance further
        session.CurrentState = OnboardingState.GenerateSite;

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("jane-doe", result);
        Assert.Contains("realestatestar.com", result);
    }
}
