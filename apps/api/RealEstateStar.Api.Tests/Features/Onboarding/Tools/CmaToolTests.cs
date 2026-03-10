using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Tools;

public class CmaToolTests
{
    [Fact]
    public async Task SubmitCmaFormTool_ReturnsConfirmation()
    {
        var tool = new SubmitCmaFormTool();
        var session = OnboardingSession.Create(null);
        var json = JsonSerializer.Deserialize<JsonElement>("""{"address":"456 Oak Ave, Newark NJ"}""");

        var result = await tool.ExecuteAsync(json, session, CancellationToken.None);

        Assert.Contains("456 Oak Ave", result);
        Assert.Contains("CMA demo submitted", result);
    }

    [Fact]
    public async Task SubmitCmaFormTool_UsesDefaultAddress_WhenNotProvided()
    {
        var tool = new SubmitCmaFormTool();
        var session = OnboardingSession.Create(null);

        var result = await tool.ExecuteAsync(default, session, CancellationToken.None);

        Assert.Contains("123 Main St", result);
    }
}
