using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Leads.OptOut;

public class OptOutEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/agents/{agentId}/leads/opt-out", Handle)
            .RequireRateLimiting("lead-opt-out");

    internal static async Task<IResult> Handle(
        string agentId,
        OptOutRequest request,
        ILeadStore leadStore,
        IMarketingConsentLog consentLog,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var lead = await leadStore.GetByEmailAsync(agentId, request.Email, ct);

        // Always return 200 — no email enumeration.
        // Only perform writes when the token matches a real lead.
        if (lead is null || lead.ConsentToken is null ||
            !string.Equals(lead.ConsentToken, request.Token, StringComparison.Ordinal))
        {
            return Results.Ok(new { status = "opted_out" });
        }

        // Idempotent: update frontmatter regardless of current opt-in state.
        await leadStore.UpdateMarketingOptInAsync(agentId, lead.Id, false, ct);

        await consentLog.RecordConsentAsync(agentId, new MarketingConsent
        {
            LeadId = lead.Id,
            Email = lead.Email,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            OptedIn = false,
            ConsentText = "Lead opted out of marketing communications.",
            Channels = ["email"],
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow,
            Action = "opt-out",
            Source = "email-unsubscribe",
        }, ct);

        return Results.Ok(new { status = "opted_out" });
    }
}
