using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class SiteDeployTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _configDir;

    public SiteDeployTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-deploy-{Guid.NewGuid():N}");
        _configDir = Path.Combine(_testDir, "config", "agents");
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private static OnboardingSession MakeSession(string? name = "Jane Doe") =>
        new()
        {
            Id = "test123",
            BearerToken = "test-token",
            Profile = new ScrapedProfile
            {
                Name = name,
                Brokerage = "RE/MAX",
                State = "NJ",
                Phone = "555-1234",
                Email = "jane@remax.com",
                LicenseId = "NJ-12345",
                PrimaryColor = "#1e40af",
                AccentColor = "#10b981",
                LogoUrl = "https://example.com/logo.png",
                ServiceAreas = ["Newark", "Jersey City"],
                OfficeAddress = "100 Broad St, Newark NJ 07102",
            },
        };

    private static CloudflareOptions ValidCloudflareOptions() =>
        new() { ApiToken = "test-token", AccountId = "test-account-id" };

    // --- Config generation tests ---

    [Fact]
    public async Task DeployAsync_WritesAgentConfigJson()
    {
        var svc = CreateService(out _);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        var configPath = Path.Combine(_configDir, "jane-doe.json");
        Assert.True(File.Exists(configPath));

        var json = await File.ReadAllTextAsync(configPath);
        Assert.Contains("\"name\": \"Jane Doe\"", json);
        Assert.Contains("\"brokerage\": \"RE/MAX\"", json);
        Assert.Contains("\"state\": \"NJ\"", json);
        Assert.Contains("\"primaryColor\": \"#1e40af\"", json);
    }

    [Fact]
    public async Task DeployAsync_SetsSessionAgentConfigId()
    {
        var svc = CreateService(out _);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        Assert.Equal("jane-doe", session.AgentConfigId);
    }

    [Fact]
    public async Task DeployAsync_WithoutProfile_Throws()
    {
        var svc = CreateService(out _);
        var session = OnboardingSession.Create(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));
    }

    // --- Slug generation tests ---

    [Theory]
    [InlineData("Jane Doe", "jane-doe")]
    [InlineData("Mary Jane Watson", "mary-jane-watson")]
    [InlineData(null, "agent")]
    public async Task DeployAsync_GeneratesCorrectSlug(string? name, string expectedSlug)
    {
        var svc = CreateService(out _);
        var session = MakeSession(name);

        await svc.DeployAsync(session, CancellationToken.None);

        Assert.Equal(expectedSlug, session.AgentConfigId);
    }

    // --- Wrangler CLI invocation tests ---

    [Fact]
    public async Task DeployAsync_InvokesWranglerWithArgumentList()
    {
        var svc = CreateService(out var processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.IsAny<ProcessStartInfo>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "https://abc123.real-estate-star-agents.pages.dev", ""));
        var session = MakeSession();

        var url = await svc.DeployAsync(session, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi =>
                psi.FileName == "npx" &&
                psi.ArgumentList.Contains("wrangler") &&
                psi.ArgumentList.Contains("pages") &&
                psi.ArgumentList.Contains("deploy") &&
                psi.ArgumentList.Contains("--project-name") &&
                psi.ArgumentList.Contains("real-estate-star-agents") &&
                psi.ArgumentList.Contains("--branch") &&
                psi.ArgumentList.Contains("jane-doe")),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task DeployAsync_ParsesPreviewUrlFromWranglerOutput()
    {
        var svc = CreateService(out var processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.IsAny<ProcessStartInfo>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0,
                "Uploading... (12/12)\n\nDeployment complete! Take a peek over at https://abc123.real-estate-star-agents.pages.dev",
                ""));
        var session = MakeSession();

        var url = await svc.DeployAsync(session, CancellationToken.None);

        Assert.Equal("https://abc123.real-estate-star-agents.pages.dev", url);
        Assert.Equal("https://abc123.real-estate-star-agents.pages.dev", session.SiteUrl);
    }

    [Fact]
    public async Task DeployAsync_FallsBackToConventionUrl_WhenNoUrlParsed()
    {
        var svc = CreateService(out var processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.IsAny<ProcessStartInfo>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "Success!", ""));
        var session = MakeSession();

        var url = await svc.DeployAsync(session, CancellationToken.None);

        Assert.Equal("https://jane-doe.real-estate-star-agents.pages.dev", url);
    }

    [Fact]
    public async Task DeployAsync_ThrowsOnWranglerFailure()
    {
        var svc = CreateService(out var processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.IsAny<ProcessStartInfo>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "Error: Authentication required"));
        var session = MakeSession();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));

        Assert.Contains("deploy", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authentication", ex.Message); // Don't leak stderr
    }

    [Fact]
    public async Task DeployAsync_PassesEnvironmentVariables()
    {
        var svc = CreateService(out var processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.IsAny<ProcessStartInfo>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "https://x.pages.dev", ""));
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi =>
                psi.Environment.ContainsKey("CLOUDFLARE_API_TOKEN") &&
                psi.Environment["CLOUDFLARE_API_TOKEN"] == "test-token" &&
                psi.Environment.ContainsKey("CLOUDFLARE_ACCOUNT_ID") &&
                psi.Environment["CLOUDFLARE_ACCOUNT_ID"] == "test-account-id"),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task DeployAsync_Uses60SecondTimeout()
    {
        var svc = CreateService(out var processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.IsAny<ProcessStartInfo>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "https://x.pages.dev", ""));
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.IsAny<ProcessStartInfo>(),
            TimeSpan.FromSeconds(60),
            It.IsAny<CancellationToken>()));
    }

    // --- Cloudflare config validation tests ---

    [Fact]
    public void CloudflareOptions_MissingApiToken_FailsValidation()
    {
        var options = new CloudflareOptions { ApiToken = "", AccountId = "acct" };
        Assert.False(options.IsValid());
    }

    [Fact]
    public void CloudflareOptions_MissingAccountId_FailsValidation()
    {
        var options = new CloudflareOptions { ApiToken = "tok", AccountId = "" };
        Assert.False(options.IsValid());
    }

    [Fact]
    public void CloudflareOptions_AllPresent_PassesValidation()
    {
        var options = ValidCloudflareOptions();
        Assert.True(options.IsValid());
    }

    // --- Helper to create service with mocked process runner ---

    private SiteDeployService CreateService(out Mock<IProcessRunner> processRunner)
    {
        processRunner = new Mock<IProcessRunner>();
        processRunner.Setup(p => p.RunAsync(
                It.IsAny<ProcessStartInfo>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "https://test.real-estate-star-agents.pages.dev", ""));

        return new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            ValidCloudflareOptions(),
            _configDir);
    }
}
