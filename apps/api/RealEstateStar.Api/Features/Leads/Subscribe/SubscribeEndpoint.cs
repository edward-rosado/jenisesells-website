using System.ComponentModel.DataAnnotations;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.Domain.Privacy;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Leads.Subscribe;

public class SubscribeEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/agents/{agentId}/leads/subscribe", Handle)
            .RequireRateLimiting("lead-opt-out");

    internal static async Task<IResult> Handle(
        string agentId,
        SubscribeRequest request,
        ILeadStore leadStore,
        IMarketingConsentLog consentLog,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var requestValidation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), requestValidation, true))
            return Results.ValidationProblem(SubmitLeadEndpoint.GroupValidationErrors(requestValidation));

        var lead = await leadStore.GetByEmailAsync(agentId, request.Email, ct);

        // Always return 200 — no email enumeration.
        // Only perform writes when the token matches a real lead.
        if (lead is null || lead.ConsentToken is null ||
            !string.Equals(lead.ConsentToken, request.Token, StringComparison.Ordinal))
        {
            return Results.Ok(new { status = "subscribed" });
        }

        // Idempotent: update frontmatter regardless of current opt-in state.
        await leadStore.UpdateMarketingOptInAsync(agentId, lead.Id, true, ct);

        await consentLog.RecordConsentAsync(agentId, new MarketingConsent
        {
            LeadId = lead.Id,
            Email = lead.Email,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            OptedIn = true,
            ConsentText = "Lead re-subscribed to marketing communications.",
            Channels = ["email"],
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow,
            Action = ConsentAction.Resubscribe,
            Source = ConsentSource.EmailLink,
        }, ct);

        return Results.Ok(new { status = "subscribed" });
    }
}
