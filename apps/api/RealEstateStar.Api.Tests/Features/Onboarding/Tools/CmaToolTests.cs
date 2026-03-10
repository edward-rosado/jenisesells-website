using System.Text.Json;
using Moq;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services;
using RealEstateStar.Api.Features.Cma.Services.Gws;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using RealEstateStar.Api.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class CmaToolTests
{
    [Fact]
    public void Name_IsSubmitCmaForm()
    {
        var tool = CreateTool(out _, out _, out _);
        Assert.Equal("submit_cma_form", tool.Name);
    }

    [Fact]
    public async Task ExecuteAsync_InvokesCmaPipeline()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        pipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            It.IsAny<string>(),
            It.IsAny<Lead>(),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UsesAgentEmailAsRecipient_InDemoMode()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        // Verify the lead email passed to the pipeline is the agent's own email (demo mode)
        pipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            It.IsAny<string>(),
            It.Is<Lead>(l => l.Email == "jane@remax.com"),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ExecuteAsync_CreatesLeadFromParameters()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>(
            """{"firstName":"Demo","lastName":"Lead","address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102","timeline":"Just curious"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        pipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            It.IsAny<string>(),
            It.Is<Lead>(l =>
                l.FirstName == "Demo" &&
                l.LastName == "Lead" &&
                l.Address == "456 Oak Ave" &&
                l.City == "Newark" &&
                l.State == "NJ" &&
                l.Zip == "07102"),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ExecuteAsync_UsesDefaultValues_WhenParametersMissing()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        pipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            It.IsAny<string>(),
            It.Is<Lead>(l =>
                l.Address == "456 Oak Ave" &&
                !string.IsNullOrEmpty(l.City) &&
                !string.IsNullOrEmpty(l.State) &&
                !string.IsNullOrEmpty(l.Zip)),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRichResultDescription()
    {
        var tool = CreateTool(out _, out _, out _);
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Contains("456 Oak Ave", result);
        Assert.Contains("email", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Drive", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSessionAgentConfigId()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        var session = MakeSessionWithProfile();
        session.AgentConfigId = "custom-agent-slug";
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        pipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            "custom-agent-slug",
            It.IsAny<Lead>(),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorMessage_OnPipelineFailure()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        pipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<CmaJob>(),
                It.IsAny<string>(),
                It.IsAny<Lead>(),
                It.IsAny<Func<CmaJobStatus, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline internal error"));
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Contains("issue", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pipeline internal error", result); // Don't leak internals
    }

    [Fact]
    public async Task ExecuteAsync_InitializesDriveFolders_OnFirstCma()
    {
        var tool = CreateTool(out _, out _, out var driveFolderInit);
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        driveFolderInit.Verify(d => d.EnsureFolderStructureAsync(
            It.IsAny<OnboardingSession>(),
            "jane@remax.com",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        var session = MakeSessionWithProfile();
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");
        using var cts = new CancellationTokenSource();

        await tool.ExecuteAsync(json, session, cts.Token);

        pipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            It.IsAny<string>(),
            It.IsAny<Lead>(),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutProfile_ReturnsError()
    {
        var tool = CreateTool(out _, out _, out _);
        var session = OnboardingSession.Create(null);
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Contains("profile", result, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers ---

    private static OnboardingSession MakeSessionWithProfile()
    {
        var session = OnboardingSession.Create(null);
        session.AgentConfigId = "jane-doe";
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Email = "jane@remax.com",
            Phone = "555-1234",
            Brokerage = "RE/MAX",
            State = "NJ",
        };
        return session;
    }

    private static SubmitCmaFormTool CreateTool(
        out Mock<ICmaPipeline> pipeline,
        out Mock<IGwsService> gwsSvc, // kept for tests that may need it later
        out Mock<IDriveFolderInitializer> driveFolderInit)
    {
        pipeline = new Mock<ICmaPipeline>();
        gwsSvc = new Mock<IGwsService>();
        driveFolderInit = new Mock<IDriveFolderInitializer>();

        return new SubmitCmaFormTool(
            pipeline.Object,
            driveFolderInit.Object);
    }
}
