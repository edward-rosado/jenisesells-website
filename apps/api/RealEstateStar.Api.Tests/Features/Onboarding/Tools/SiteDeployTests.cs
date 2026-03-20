using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
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
        Assert.Contains("\"id\": \"jane-doe\"", json);
        Assert.Contains("\"name\": \"Jane Doe\"", json);
        Assert.Contains("\"brokerage\": \"RE/MAX\"", json);
        Assert.Contains("\"state\": \"NJ\"", json);
        Assert.Contains("\"primary_color\": \"#1e40af\"", json);
    }

    [Fact]
    public async Task DeployAsync_WritesAgentContentJson()
    {
        var svc = CreateService(out _);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        var contentPath = Path.Combine(_configDir, "jane-doe.content.json");
        Assert.True(File.Exists(contentPath));

        var json = await File.ReadAllTextAsync(contentPath);
        Assert.Contains("\"template\": \"emerald-classic\"", json);
        Assert.Contains("\"hero\"", json);
        Assert.Contains("\"services\"", json);
        Assert.Contains("\"about\"", json);
        Assert.Contains("Jane Doe", json);
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

    // --- Build step invocation tests ---

    [Fact]
    public async Task DeployAsync_InvokesNextBuild()
    {
        var svc = CreateService(out var processRunner);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi =>
                psi.FileName == "npx" &&
                psi.ArgumentList.Contains("next") &&
                psi.ArgumentList.Contains("build")),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_InvokesOpenNextBuild()
    {
        var svc = CreateService(out var processRunner);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi =>
                psi.FileName == "npx" &&
                psi.ArgumentList.Contains("opennextjs-cloudflare") &&
                psi.ArgumentList.Contains("build")),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeployAsync_ThrowsOnNextBuildFailure()
    {
        var processRunner = new Mock<IProcessRunner>();
        // First call (next build) fails
        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("next")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "Build error"));

        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            ValidCloudflareOptions(),
            _configDir);
        var session = MakeSession();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));

        Assert.Contains("Next.js build failed", ex.Message);
    }

    [Fact]
    public async Task DeployAsync_ThrowsOnOpenNextBuildFailure()
    {
        var processRunner = new Mock<IProcessRunner>();
        // next build succeeds
        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi =>
                    psi.ArgumentList.Contains("next") && psi.ArgumentList.Contains("build")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));
        // opennext build fails
        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("opennextjs-cloudflare")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "OpenNext error"));

        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            ValidCloudflareOptions(),
            _configDir);
        var session = MakeSession();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));

        Assert.Contains("OpenNext build failed", ex.Message);
    }

    // --- Wrangler CLI invocation tests ---

    [Fact]
    public async Task DeployAsync_InvokesWranglerWithArgumentList()
    {
        var svc = CreateService(out var processRunner);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi =>
                psi.FileName == "npx" &&
                psi.ArgumentList.Contains("wrangler") &&
                psi.ArgumentList.Contains("pages") &&
                psi.ArgumentList.Contains("deploy") &&
                psi.ArgumentList.Contains(".open-next/assets") &&
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
        var processRunner = new Mock<IProcessRunner>();
        SetupBuildSteps(processRunner);
        // Wrangler returns preview URL
        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("wrangler")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0,
                "Uploading... (12/12)\n\nDeployment complete! Take a peek over at https://abc123.real-estate-star-agents.pages.dev",
                ""));

        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            ValidCloudflareOptions(),
            _configDir);
        var session = MakeSession();

        var url = await svc.DeployAsync(session, CancellationToken.None);

        Assert.Equal("https://abc123.real-estate-star-agents.pages.dev", url);
        Assert.Equal("https://abc123.real-estate-star-agents.pages.dev", session.SiteUrl);
    }

    [Fact]
    public async Task DeployAsync_FallsBackToConventionUrl_WhenNoUrlParsed()
    {
        var processRunner = new Mock<IProcessRunner>();
        SetupBuildSteps(processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("wrangler")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "Success!", ""));

        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            ValidCloudflareOptions(),
            _configDir);
        var session = MakeSession();

        var url = await svc.DeployAsync(session, CancellationToken.None);

        Assert.Equal("https://jane-doe.real-estate-star-agents.pages.dev", url);
    }

    [Fact]
    public async Task DeployAsync_ThrowsOnWranglerFailure()
    {
        var processRunner = new Mock<IProcessRunner>();
        SetupBuildSteps(processRunner);
        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("wrangler")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "Error: Authentication required"));

        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            ValidCloudflareOptions(),
            _configDir);
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
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi =>
                psi.ArgumentList.Contains("wrangler") &&
                psi.Environment.ContainsKey("CLOUDFLARE_API_TOKEN") &&
                psi.Environment["CLOUDFLARE_API_TOKEN"] == "test-token" &&
                psi.Environment.ContainsKey("CLOUDFLARE_ACCOUNT_ID") &&
                psi.Environment["CLOUDFLARE_ACCOUNT_ID"] == "test-account-id"),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task DeployAsync_UsesCorrectTimeouts()
    {
        var svc = CreateService(out var processRunner);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        // Build steps use 120s timeout
        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("next")),
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()));

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("opennextjs-cloudflare")),
            TimeSpan.FromSeconds(120),
            It.IsAny<CancellationToken>()));

        // Wrangler deploy uses 60s timeout
        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("wrangler")),
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

    // --- Cloudflare config missing throws tests ---

    [Fact]
    public async Task DeployAsync_MissingApiToken_ThrowsInvalidOperationException()
    {
        var processRunner = new Mock<IProcessRunner>();
        var options = new CloudflareOptions { ApiToken = "", AccountId = "acct-123" };
        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            options,
            _configDir);
        var session = MakeSession();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));

        Assert.Contains("ApiToken", ex.Message);
    }

    [Fact]
    public async Task DeployAsync_WhitespaceApiToken_ThrowsInvalidOperationException()
    {
        var processRunner = new Mock<IProcessRunner>();
        var options = new CloudflareOptions { ApiToken = "   ", AccountId = "acct-123" };
        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            options,
            _configDir);
        var session = MakeSession();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));

        Assert.Contains("ApiToken", ex.Message);
    }

    [Fact]
    public async Task DeployAsync_MissingAccountId_ThrowsInvalidOperationException()
    {
        var processRunner = new Mock<IProcessRunner>();
        var options = new CloudflareOptions { ApiToken = "valid-token", AccountId = "" };
        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            options,
            _configDir);
        var session = MakeSession();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));

        Assert.Contains("AccountId", ex.Message);
    }

    [Fact]
    public async Task DeployAsync_WhitespaceAccountId_ThrowsInvalidOperationException()
    {
        var processRunner = new Mock<IProcessRunner>();
        var options = new CloudflareOptions { ApiToken = "valid-token", AccountId = "   " };
        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            options,
            _configDir);
        var session = MakeSession();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeployAsync(session, CancellationToken.None));

        Assert.Contains("AccountId", ex.Message);
    }

    [Fact]
    public async Task DeployAsync_NullServiceAreas_DefaultsToEmptyArray()
    {
        var svc = CreateService(out _);
        var session = MakeSession();
        // Create a profile with null ServiceAreas to hit the ?? [] branch
        session.Profile = new ScrapedProfile
        {
            Name = "Test Agent",
            Brokerage = "RE/MAX",
            State = "NJ",
            Phone = "555-0000",
            Email = "test@example.com",
            LicenseId = "NJ-00000",
            ServiceAreas = null,
            OfficeAddress = null,
            PrimaryColor = null,
            AccentColor = null,
            LogoUrl = null,
        };

        await svc.DeployAsync(session, CancellationToken.None);

        var configPath = Path.Combine(_configDir, "test-agent.json");
        var json = await File.ReadAllTextAsync(configPath);
        Assert.Contains("\"service_areas\": []", json);
        // Null colors should use defaults
        Assert.Contains("#1e40af", json);
        Assert.Contains("#10b981", json);
    }

    [Fact]
    public async Task DeployAsync_CallsBuildStepsBeforeWranglerDeploy()
    {
        var callOrder = new List<string>();
        var processRunner = new Mock<IProcessRunner>();

        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi =>
                    psi.ArgumentList.Contains("next") && psi.ArgumentList.Contains("build")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("next-build"))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("opennextjs-cloudflare")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("opennext-build"))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("wrangler")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("wrangler-deploy"))
            .ReturnsAsync(new ProcessResult(0, "https://test.real-estate-star-agents.pages.dev", ""));

        var svc = new SiteDeployService(
            NullLogger<SiteDeployService>.Instance,
            processRunner.Object,
            ValidCloudflareOptions(),
            _configDir);
        var session = MakeSession();

        await svc.DeployAsync(session, CancellationToken.None);

        Assert.Equal(["next-build", "opennext-build", "wrangler-deploy"], callOrder);
    }

    // --- Helper to create service with mocked process runner ---

    /// <summary>
    /// Sets up next build and opennext build to succeed (used by tests that
    /// manually configure the wrangler step).
    /// </summary>
    private static void SetupBuildSteps(Mock<IProcessRunner> processRunner)
    {
        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi =>
                    psi.ArgumentList.Contains("next") && psi.ArgumentList.Contains("build")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));

        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("opennextjs-cloudflare")),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", ""));
    }

    private SiteDeployService CreateService(out Mock<IProcessRunner> processRunner)
    {
        processRunner = new Mock<IProcessRunner>();

        // Default: all three steps succeed
        SetupBuildSteps(processRunner);

        processRunner.Setup(p => p.RunAsync(
                It.Is<ProcessStartInfo>(psi => psi.ArgumentList.Contains("wrangler")),
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
