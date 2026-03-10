using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class SiteDeployTests : IDisposable
{
    private readonly string _testDir;

    public SiteDeployTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task DeployAsync_SetsSessionSiteUrl()
    {
        var service = new SiteDeployService(NullLogger<SiteDeployService>.Instance);
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Brokerage = "RE/MAX",
            State = "NJ",
            PrimaryColor = "#1e40af",
        };

        var url = await service.DeployAsync(session, CancellationToken.None);

        Assert.Contains("jane-doe", url);
        Assert.Contains("realestatestar.com", url);
        Assert.NotNull(session.SiteUrl);
        Assert.Equal("jane-doe", session.AgentConfigId);
    }

    [Fact]
    public async Task DeployAsync_WithoutProfile_Throws()
    {
        var service = new SiteDeployService(NullLogger<SiteDeployService>.Instance);
        var session = OnboardingSession.Create(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeployAsync(session, CancellationToken.None));
    }
}
