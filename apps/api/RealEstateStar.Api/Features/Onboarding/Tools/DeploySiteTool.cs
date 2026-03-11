using System.Text.Json;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class DeploySiteTool(ISiteDeployService siteDeployService, OnboardingStateMachine stateMachine) : IOnboardingTool
{
    public string Name => "deploy_site";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        try
        {
            var siteUrl = await siteDeployService.DeployAsync(session, ct);
            session.SiteUrl = siteUrl;

            // Advance past PreviewSite to DemoCma (preview is the deployed URL)
            if (stateMachine.CanAdvance(session, OnboardingState.PreviewSite))
                stateMachine.Advance(session, OnboardingState.PreviewSite);
            if (stateMachine.CanAdvance(session, OnboardingState.DemoCma))
                stateMachine.Advance(session, OnboardingState.DemoCma);

            return $"SUCCESS: Site deployed and live at {siteUrl}. The agent can visit this URL now.";
        }
        catch (Exception)
        {
            // Advance past deploy so the flow doesn't get stuck — CMA demo can still run
            if (stateMachine.CanAdvance(session, OnboardingState.PreviewSite))
                stateMachine.Advance(session, OnboardingState.PreviewSite);
            if (stateMachine.CanAdvance(session, OnboardingState.DemoCma))
                stateMachine.Advance(session, OnboardingState.DemoCma);

            return "FAILED: Site deployment failed due to a configuration issue. Tell the agent honestly that site deployment is not available yet and the team will set it up. Move on to the CMA demo.";
        }
    }
}
