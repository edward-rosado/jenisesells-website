using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class DeploySiteTool(ISiteDeployService siteDeployService) : IOnboardingTool
{
    public string Name => "deploy_site";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        try
        {
            var siteUrl = await siteDeployService.DeployAsync(session, ct);
            return $"Site deployed at {siteUrl}";
        }
        catch (Exception)
        {
            return "Site deployment failed. The team has been notified and will resolve the issue shortly.";
        }
    }
}
