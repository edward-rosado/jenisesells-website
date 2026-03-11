using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class SubmitCmaFormTool(
    ICmaPipeline cmaPipeline,
    IDriveFolderInitializer driveFolderInitializer,
    OnboardingStateMachine stateMachine,
    ILogger<SubmitCmaFormTool> logger) : IOnboardingTool
{
    public string Name => "submit_cma_form";

    /// <summary>
    /// Human-readable labels for each CMA pipeline step, shown in the progress card.
    /// </summary>
    internal static readonly Dictionary<CmaJobStatus, string> StepLabels = new()
    {
        [CmaJobStatus.SearchingComps] = "Searching comparable sales",
        [CmaJobStatus.ResearchingLead] = "Researching property records",
        [CmaJobStatus.Analyzing] = "Analyzing market trends",
        [CmaJobStatus.GeneratingPdf] = "Generating PDF report",
        [CmaJobStatus.OrganizingDrive] = "Organizing in Google Drive",
        [CmaJobStatus.SendingEmail] = "Emailing report",
        [CmaJobStatus.Logging] = "Logging lead",
        [CmaJobStatus.Complete] = "Complete",
    };

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var profile = session.Profile;
        if (profile is null)
            return "Cannot submit CMA demo — agent profile is not set up yet.";

        var agentEmail = profile.Email ?? "";
        var agentId = session.AgentConfigId ?? OnboardingHelpers.GenerateSlug(profile.Name);

        try
        {
            // Build lead from parameters — demo mode uses agent's own email as recipient
            var lead = BuildLeadFromParameters(parameters, agentEmail, profile);

            // Ensure Drive folder structure exists (idempotent — only runs once per session)
            if (!string.IsNullOrEmpty(agentEmail))
                await driveFolderInitializer.EnsureFolderStructureAsync(session, agentEmail, ct);

            // Create CMA job and run the pipeline
            var job = CmaJob.Create(agentId, lead);
            await cmaPipeline.ExecuteAsync(job, agentId, lead, _ => Task.CompletedTask, ct);

            var address = lead.FullAddress;

            // Advance to ShowResults after successful CMA demo
            if (stateMachine.CanAdvance(session, OnboardingState.ShowResults))
                stateMachine.Advance(session, OnboardingState.ShowResults);

            logger.LogInformation("[CMA-TOOL-001] CMA demo completed for session {SessionId}, address {Address}",
                session.Id, address);

            // Return a card marker so the frontend can render a CMA progress card
            var cardJson = BuildProgressCardJson(address, agentEmail, "complete");
            return $"[CARD:cma_progress]{cardJson} " +
                   $"SUCCESS: CMA pipeline completed for {address}. " +
                   $"Report emailed to {agentEmail}, Lead Brief saved to Google Drive, lead logged in tracking sheet.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CMA-TOOL-002] CMA demo failed for session {SessionId}. ExType={ExType}",
                session.Id, ex.GetType().Name);

            // Advance past DemoCma so the flow doesn't get stuck
            if (stateMachine.CanAdvance(session, OnboardingState.ShowResults))
                stateMachine.Advance(session, OnboardingState.ShowResults);

            return "FAILED: CMA demo could not run — the pipeline encountered an error. " +
                   "Tell the agent honestly that the CMA demo is not working right now and the team will fix it. " +
                   "Do NOT claim emails were sent or files were created.";
        }
    }

    internal static string BuildProgressCardJson(string address, string recipientEmail, string status)
    {
        var steps = StepLabels.Select(kvp => new
        {
            label = kvp.Value,
            status = status == "complete" ? "done" : "pending"
        });

        var card = new
        {
            address,
            recipientEmail,
            status,
            steps = steps.ToArray()
        };

        return JsonSerializer.Serialize(card);
    }

    internal static Lead BuildLeadFromParameters(JsonElement parameters, string agentEmail, ScrapedProfile profile)
    {
        var firstName = GetStringProperty(parameters, "firstName") ?? "Demo";
        var lastName = GetStringProperty(parameters, "lastName") ?? "Seller";
        var address = GetStringProperty(parameters, "address") ?? "123 Main St";
        var city = GetStringProperty(parameters, "city") ?? profile.State switch
        {
            "NJ" => "Newark",
            "NY" => "New York",
            "CA" => "Los Angeles",
            _ => "Springfield"
        };
        var state = GetStringProperty(parameters, "state") ?? profile.State ?? "NJ";
        var zip = GetStringProperty(parameters, "zip") ?? "07102";
        var timeline = GetStringProperty(parameters, "timeline") ?? "Just curious";
        var phone = GetStringProperty(parameters, "phone") ?? "555-000-0000";

        return new Lead
        {
            FirstName = firstName,
            LastName = lastName,
            Email = agentEmail, // Demo mode: send to agent's own email
            Phone = phone,
            Address = address,
            City = city,
            State = state,
            Zip = zip,
            Timeline = timeline,
            Beds = GetIntProperty(parameters, "beds"),
            Baths = GetIntProperty(parameters, "baths"),
            Sqft = GetIntProperty(parameters, "sqft"),
        };
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var prop))
            return prop.GetString();
        return null;
    }

    private static int? GetIntProperty(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var value))
            return value;
        return null;
    }
}
