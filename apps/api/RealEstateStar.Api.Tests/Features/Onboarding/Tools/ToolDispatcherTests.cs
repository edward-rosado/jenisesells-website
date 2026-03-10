using System.Text.Json;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

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

        var dispatcher = new ToolDispatcher([mockTool.Object]);
        var session = OnboardingSession.Create(null);

        var result = await dispatcher.DispatchAsync("test_tool", default, session, CancellationToken.None);

        Assert.Equal("tool executed", result);
    }

    [Fact]
    public async Task DispatchAsync_UnknownTool_Throws()
    {
        var dispatcher = new ToolDispatcher([]);
        var session = OnboardingSession.Create(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync("nonexistent", default, session, CancellationToken.None));
    }

    [Fact]
    public void HasTool_ReturnsTrueForRegisteredTool()
    {
        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("my_tool");
        var dispatcher = new ToolDispatcher([mockTool.Object]);

        Assert.True(dispatcher.HasTool("my_tool"));
        Assert.False(dispatcher.HasTool("other_tool"));
    }

    [Fact]
    public async Task UpdateProfileTool_UpdatesSessionProfile()
    {
        var tool = new UpdateProfileTool();
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
        var tool = new DeploySiteTool();
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Jane Doe" };

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.NotNull(session.SiteUrl);
        Assert.Contains("jane-doe", session.SiteUrl);
        Assert.Contains("realestatestar.com", result);
    }
}
