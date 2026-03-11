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

        Assert.StartsWith("FAILED:", result);
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

    // --- Missing branch coverage ---

    [Theory]
    [InlineData("NY", "New York")]
    [InlineData("CA", "Los Angeles")]
    [InlineData("TX", "Springfield")]
    public void BuildLeadFromParameters_CityDefaultsByState(string state, string expectedCity)
    {
        var profile = new ScrapedProfile { Name = "Test Agent", State = state };
        // Pass a JSON object with no "city" key so the switch default fires
        var parameters = JsonSerializer.Deserialize<JsonElement>("""{"address":"1 Test St"}""");

        var lead = SubmitCmaFormTool.BuildLeadFromParameters(parameters, "agent@test.com", profile);

        Assert.Equal(expectedCity, lead.City);
    }

    [Fact]
    public async Task ExecuteAsync_NullEmail_SkipsDriveInit()
    {
        var tool = CreateTool(out _, out _, out var driveFolderInit);
        var session = OnboardingSession.Create(null);
        session.AgentConfigId = "jane-doe";
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Email = null, // null email — should skip drive folder init
            State = "NJ",
        };
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        driveFolderInit.Verify(d => d.EnsureFolderStructureAsync(
            It.IsAny<OnboardingSession>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NullAgentConfigId_UsesSlugFromProfileName()
    {
        var tool = CreateTool(out var pipeline, out _, out _);
        var session = OnboardingSession.Create(null);
        session.AgentConfigId = null; // null — falls back to GenerateSlug(profile.Name)
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Email = "jane@remax.com",
            State = "NJ",
        };
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave","city":"Newark","state":"NJ","zip":"07102"}""");

        await tool.ExecuteAsync(json, session, CancellationToken.None);

        // GenerateSlug("Jane Doe") → "jane-doe"
        pipeline.Verify(p => p.ExecuteAsync(
            It.IsAny<CmaJob>(),
            "jane-doe",
            It.IsAny<Lead>(),
            It.IsAny<Func<CmaJobStatus, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BuildLeadFromParameters_WithIntProperties_ParsesCorrectly()
    {
        var profile = new ScrapedProfile { Name = "Test Agent", State = "NJ" };
        var parameters = JsonSerializer.Deserialize<JsonElement>(
            """{"address":"1 Main St","beds":3,"baths":2,"sqft":1500}""");

        var lead = SubmitCmaFormTool.BuildLeadFromParameters(parameters, "agent@test.com", profile);

        Assert.Equal(3, lead.Beds);
        Assert.Equal(2, lead.Baths);
        Assert.Equal(1500, lead.Sqft);
    }

    [Fact]
    public void BuildLeadFromParameters_WithNonObjectJson_UsesDefaults()
    {
        var profile = new ScrapedProfile { Name = "Test Agent", State = "NJ" };
        // A JSON string value — ValueKind != Object, so all GetStringProperty/GetIntProperty calls return null
        var parameters = JsonSerializer.Deserialize<JsonElement>(""""
            "not-an-object"
            """");

        var lead = SubmitCmaFormTool.BuildLeadFromParameters(parameters, "agent@test.com", profile);

        // All fields should fall back to defaults
        Assert.Equal("Demo", lead.FirstName);
        Assert.Equal("Seller", lead.LastName);
        Assert.Equal("123 Main St", lead.Address);
        Assert.Equal("Newark", lead.City); // NJ default
        Assert.Equal("NJ", lead.State);
        Assert.Equal("07102", lead.Zip);
        Assert.Null(lead.Beds);
        Assert.Null(lead.Baths);
        Assert.Null(lead.Sqft);
    }

    [Fact]
    public void BuildLeadFromParameters_MissingIntProperty_ReturnsNull()
    {
        var profile = new ScrapedProfile { Name = "Test Agent", State = "NJ" };
        // Object with no beds/baths/sqft — TryGetProperty returns false for each
        var parameters = JsonSerializer.Deserialize<JsonElement>("""{"address":"1 Main St"}""");

        var lead = SubmitCmaFormTool.BuildLeadFromParameters(parameters, "agent@test.com", profile);

        Assert.Null(lead.Beds);
        Assert.Null(lead.Baths);
        Assert.Null(lead.Sqft);
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
