using FluentAssertions;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Tests.Services;

public class AgentConfigServiceTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "config", "agent.schema.json")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    private static AgentConfigService CreateService()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "agents");
        return new AgentConfigService(configDir);
    }

    [Fact]
    public async Task LoadAgent_ReturnsConfig_ForValidId()
    {
        var service = CreateService();

        var config = await service.GetAgentAsync("jenise-buckalew");

        config.Should().NotBeNull();
        config!.Id.Should().Be("jenise-buckalew");
        config.Identity.Should().NotBeNull();
        config.Identity!.Name.Should().Be("Jenise Buckalew");
        config.Identity.Email.Should().Be("jenisesellsnj@gmail.com");
        config.Identity.Phone.Should().Be("(347) 393-5993");
        config.Identity.LicenseId.Should().Be("0676823");
        config.Identity.Brokerage.Should().Be("Green Light Realty LLC");
        config.Identity.Languages.Should().Contain("Spanish");
        config.Location.Should().NotBeNull();
        config.Location!.State.Should().Be("NJ");
        config.Location.ServiceAreas.Should().Contain("Middlesex County");
        config.Branding.Should().NotBeNull();
        config.Branding!.PrimaryColor.Should().Be("#1B5E20");
        config.Branding.AccentColor.Should().Be("#C8A951");
        config.Branding.FontFamily.Should().Be("Segoe UI");
        config.Integrations.Should().NotBeNull();
        config.Integrations!.EmailProvider.Should().Be("gmail");
        config.Compliance.Should().NotBeNull();
        config.Compliance!.StateForm.Should().Be("NJ-REALTORS-118");
    }

    [Fact]
    public async Task LoadAgent_ReturnsNull_ForUnknownId()
    {
        var service = CreateService();

        var config = await service.GetAgentAsync("nonexistent-agent");

        config.Should().BeNull();
    }
}
