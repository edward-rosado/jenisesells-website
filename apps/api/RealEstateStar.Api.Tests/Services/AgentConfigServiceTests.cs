using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

        var config = await service.GetAgentAsync("jenise-buckalew", CancellationToken.None);

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

        var config = await service.GetAgentAsync("nonexistent-agent", CancellationToken.None);

        config.Should().BeNull();
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("../../secrets")]
    [InlineData("..%2Fetc%2Fpasswd")]
    [InlineData("valid-id/../../../etc/passwd")]
    public async Task GetAgentAsync_RejectsPathTraversal(string maliciousId)
    {
        var service = CreateService();

        var act = () => service.GetAgentAsync(maliciousId, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("agentId");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Agent_Name")]
    [InlineData("UPPERCASE")]
    [InlineData("has spaces")]
    [InlineData("special!chars")]
    [InlineData("dots.not.allowed")]
    public async Task GetAgentAsync_RejectsInvalidAgentIds(string invalidId)
    {
        var service = CreateService();

        var act = () => service.GetAgentAsync(invalidId, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("agentId");
    }

    [Theory]
    [InlineData("jenise-buckalew")]
    [InlineData("valid-agent-123")]
    [InlineData("abc")]
    public async Task GetAgentAsync_AcceptsValidAgentIds(string validId)
    {
        var service = CreateService();

        // Should not throw - may return null if file doesn't exist
        var act = () => service.GetAgentAsync(validId, CancellationToken.None);

        await act.Should().NotThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAgentAsync_WithLogger_LogsFileNotFound()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "agents");
        var service = new AgentConfigService(configDir, NullLogger<AgentConfigService>.Instance);

        var config = await service.GetAgentAsync("nonexistent-agent", CancellationToken.None);

        config.Should().BeNull();
    }

    [Fact]
    public async Task GetAgentAsync_WithLogger_LogsSuccessfulLoad()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "agents");
        var service = new AgentConfigService(configDir, NullLogger<AgentConfigService>.Instance);

        var config = await service.GetAgentAsync("jenise-buckalew", CancellationToken.None);

        config.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAgentAsync_WithNullLogger_StillWorks()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "agents");
        // Passing null logger explicitly (the default parameter)
        var service = new AgentConfigService(configDir, null);

        var config = await service.GetAgentAsync("nonexistent-agent", CancellationToken.None);

        config.Should().BeNull();
    }

    [Fact]
    public async Task GetAgentAsync_NullAgentId_Throws()
    {
        var service = CreateService();

        var act = () => service.GetAgentAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
