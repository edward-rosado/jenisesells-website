using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class SubmitCmaFormTool : IOnboardingTool
{
    public string Name => "submit_cma_form";

    public Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct)
    {
        // TODO: Wire to actual CMA pipeline endpoint.
        // For now, return a stub response simulating the demo.
        var address = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("address", out var a)
            ? a.GetString() ?? "123 Main St, Anytown NJ"
            : "123 Main St, Anytown NJ";

        return Task.FromResult(
            $"CMA demo submitted for {address}. " +
            "The report will include comparable sales, market analysis, and a recommended list price. " +
            "Check your email inbox and Google Drive for the completed report.");
    }
}
