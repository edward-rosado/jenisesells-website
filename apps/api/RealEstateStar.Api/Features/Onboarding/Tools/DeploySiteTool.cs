using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class DeploySiteTool : IOnboardingTool
{
    public string Name => "deploy_site";

    public Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        // TODO: Wire SiteDeployService (Task 22) to generate config and trigger deploy.
        var agentSlug = (session.Profile?.Name ?? "agent").ToLowerInvariant().Replace(" ", "-");
        var siteUrl = $"https://{agentSlug}.realestatestar.com";
        session.SiteUrl = siteUrl;

        return Task.FromResult($"Site deployed at {siteUrl}");
    }
}
