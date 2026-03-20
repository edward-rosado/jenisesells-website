using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Services.Gws;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class DriveFolderInitializer(
    IGwsService gwsService,
    ILogger<DriveFolderInitializer> logger) : IDriveFolderInitializer
{
    private static readonly string[] AllFolders =
    [
        "Real Estate Star/1 - Leads",
        "Real Estate Star/2 - Active Clients",
        "Real Estate Star/3 - Under Contract",
        "Real Estate Star/4 - Closed",
        "Real Estate Star/5 - Inactive",
        "Real Estate Star/5 - Inactive/Dead Leads",
        "Real Estate Star/5 - Inactive/Expired Clients",
        "Real Estate Star/6 - Referral Network",
        "Real Estate Star/6 - Referral Network/Agents",
        "Real Estate Star/6 - Referral Network/Brokerages",
        "Real Estate Star/6 - Referral Network/Summary",
    ];

    public async Task EnsureFolderStructureAsync(OnboardingSession session, string agentEmail, CancellationToken ct)
    {
        if (session.DriveFolderInitialized)
        {
            logger.LogDebug("Drive folder structure already initialized for {AgentEmail}", agentEmail);
            return;
        }

        logger.LogInformation("Initializing Drive folder structure for {AgentEmail}", agentEmail);

        foreach (var folder in AllFolders)
        {
            try
            {
                await gwsService.CreateDriveFolderAsync(agentEmail, folder, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create Drive folder {Folder} for {AgentEmail} — continuing",
                    folder, agentEmail);
            }
        }

        session.DriveFolderInitialized = true;

        logger.LogInformation("Drive folder structure initialized for {AgentEmail} ({Count} folders)",
            agentEmail, AllFolders.Length);
    }
}
