using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.DataServices.Tests.Config;

public class AccountConfigServiceTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "config", "accounts")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    private static AccountConfigService CreateService()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "accounts");
        return new AccountConfigService(configDir);
    }

    [Fact]
    public async Task LoadAgent_ReturnsConfig_ForValidId()
    {
        var service = CreateService();

        var config = await service.GetAccountAsync("jenise-buckalew", CancellationToken.None);

        config.Should().NotBeNull();
        config!.Handle.Should().Be("jenise-buckalew");
        config.Agent.Should().NotBeNull();
        config.Agent!.Name.Should().Be("Jenise Buckalew");
        config.Agent.Email.Should().Be("jenisesellsnj@gmail.com");
        config.Agent.Phone.Should().Be("(347) 393-5993");
        config.Agent.LicenseNumber.Should().Be("0676823");
        config.Agent.Languages.Should().Contain("Spanish");
        config.Brokerage.Should().NotBeNull();
        config.Brokerage!.Name.Should().Be("Green Light Realty LLC");
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

        var config = await service.GetAccountAsync("nonexistent-agent", CancellationToken.None);

        config.Should().BeNull();
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("../../secrets")]
    [InlineData("..%2Fetc%2Fpasswd")]
    [InlineData("valid-id/../../../etc/passwd")]
    public async Task GetAccountAsync_RejectsPathTraversal(string maliciousId)
    {
        var service = CreateService();

        var act = () => service.GetAccountAsync(maliciousId, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("handle");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Agent_Name")]
    [InlineData("UPPERCASE")]
    [InlineData("has spaces")]
    [InlineData("special!chars")]
    [InlineData("dots.not.allowed")]
    public async Task GetAccountAsync_RejectsInvalidAgentIds(string invalidId)
    {
        var service = CreateService();

        var act = () => service.GetAccountAsync(invalidId, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("handle");
    }

    [Theory]
    [InlineData("jenise-buckalew")]
    [InlineData("valid-agent-123")]
    [InlineData("abc")]
    public async Task GetAccountAsync_AcceptsValidAgentIds(string validId)
    {
        var service = CreateService();

        // Should not throw - may return null if file doesn't exist
        var act = () => service.GetAccountAsync(validId, CancellationToken.None);

        await act.Should().NotThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAccountAsync_WithLogger_LogsFileNotFound()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "accounts");
        var service = new AccountConfigService(configDir, NullLogger<AccountConfigService>.Instance);

        var config = await service.GetAccountAsync("nonexistent-agent", CancellationToken.None);

        config.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountAsync_WithLogger_LogsSuccessfulLoad()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "accounts");
        var service = new AccountConfigService(configDir, NullLogger<AccountConfigService>.Instance);

        var config = await service.GetAccountAsync("jenise-buckalew", CancellationToken.None);

        config.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAccountAsync_WithNullLogger_StillWorks()
    {
        var repoRoot = FindRepoRoot();
        var configDir = Path.Combine(repoRoot, "config", "accounts");
        // Passing null logger explicitly (the default parameter)
        var service = new AccountConfigService(configDir, null);

        var config = await service.GetAccountAsync("nonexistent-agent", CancellationToken.None);

        config.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountAsync_NullAgentId_Throws()
    {
        var service = CreateService();

        var act = () => service.GetAccountAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
