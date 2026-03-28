using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class DeploySiteToolTests
{
    [Fact]
    public void Name_IsDeploySite()
    {
        var tool = CreateTool(out _);
        Assert.Equal("deploy_site", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_CallsSiteDeployService()
    {
        var tool = CreateTool(out var deploySvc);
        deploySvc.Setup(d => d.DeployAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://jane-doe.pages.dev");
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Jane Doe" };
        session.CurrentState = OnboardingState.GenerateSite;

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        deploySvc.Verify(d => d.DeployAsync(session, CancellationToken.None), Times.Once);
        Assert.Contains("https://jane-doe.pages.dev", result);
        Assert.Equal(OnboardingState.ConnectGoogle, session.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_AdvancesToConnectGoogle_OnFailure()
    {
        var tool = CreateTool(out var deploySvc);
        deploySvc.Setup(d => d.DeployAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Deploy failed internally"));
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Jane Doe" };
        session.CurrentState = OnboardingState.GenerateSite;

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.StartsWith("FAILED:", result);
        Assert.DoesNotContain("Deploy failed internally", result);
        Assert.Equal(OnboardingState.ConnectGoogle, session.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        var tool = CreateTool(out var deploySvc);
        deploySvc.Setup(d => d.DeployAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://test.pages.dev");
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile { Name = "Test" };
        session.CurrentState = OnboardingState.GenerateSite;
        using var cts = new CancellationTokenSource();

        await tool.ExecuteAsync(default, session, cts.Token);

        deploySvc.Verify(d => d.DeployAsync(session, cts.Token));
    }

    private static DeploySiteTool CreateTool(out Mock<ISiteDeployService> deploySvc)
    {
        deploySvc = new Mock<ISiteDeployService>();
        deploySvc.Setup(d => d.DeployAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://default.pages.dev");
        return new DeploySiteTool(deploySvc.Object, new OnboardingStateMachine(), NullLogger<DeploySiteTool>.Instance);
    }
}
