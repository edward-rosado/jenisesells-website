using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class DeploySiteTool(ISiteDeployService siteDeployService, OnboardingStateMachine stateMachine, ILogger<DeploySiteTool> logger) : IOnboardingTool
{
    public string Name => "deploy_site";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        try
        {
            var siteUrl = await siteDeployService.DeployAsync(session, ct);
            session.SiteUrl = siteUrl;

            // Advance to ConnectGoogle
            if (stateMachine.CanAdvance(session, OnboardingState.ConnectGoogle))
                stateMachine.Advance(session, OnboardingState.ConnectGoogle);

            return $"SUCCESS: Site deployed and live at {siteUrl}. The agent can visit this URL now.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DEPLOY-TOOL-001] Site deployment failed for session {SessionId}", session.Id);

            // Advance past deploy so the flow doesn't get stuck
            if (stateMachine.CanAdvance(session, OnboardingState.ConnectGoogle))
                stateMachine.Advance(session, OnboardingState.ConnectGoogle);

            return "FAILED: Site deployment failed due to a configuration issue. Tell the agent honestly that site deployment is not available yet and the team will set it up. Move on to connecting Google.";
        }
    }
}
