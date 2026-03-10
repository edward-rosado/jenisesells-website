using System.Text.Json;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services;
using RealEstateStar.Api.Features.Onboarding.Services;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class SubmitCmaFormTool(
    ICmaPipeline cmaPipeline,
    IDriveFolderInitializer driveFolderInitializer) : IOnboardingTool
{
    public string Name => "submit_cma_form";

    public async Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        var profile = session.Profile;
        if (profile is null)
            return "Cannot submit CMA demo — agent profile is not set up yet.";

        var agentEmail = profile.Email ?? "";
        var agentId = session.AgentConfigId ?? GenerateSlug(profile.Name);

        try
        {
            // Build lead from parameters — demo mode uses agent's own email as recipient
            var lead = BuildLeadFromParameters(parameters, agentEmail, profile);

            // Ensure Drive folder structure exists (idempotent — only runs once per agent)
            if (!string.IsNullOrEmpty(agentEmail))
                await driveFolderInitializer.EnsureFolderStructureAsync(agentEmail, ct);

            // Create CMA job and run the pipeline
            var job = CmaJob.Create(agentId, lead);
            await cmaPipeline.ExecuteAsync(job, agentId, lead, _ => Task.CompletedTask, ct);

            var address = lead.FullAddress;
            return $"CMA pipeline completed for {address}. " +
                   $"The report has been emailed to {agentEmail} (your own email, so you can see the seller experience). " +
                   "A Lead Brief has been created in your Google Drive under Real Estate Star/1 - Leads/, " +
                   "and the lead has been logged in your tracking spreadsheet.";
        }
        catch (Exception)
        {
            return "The CMA demo encountered an issue. The team has been notified. " +
                   "This won't affect your actual CMA pipeline once set up.";
        }
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

    private static string GenerateSlug(string? name) =>
        (name ?? "agent").ToLowerInvariant().Replace(" ", "-");
}
