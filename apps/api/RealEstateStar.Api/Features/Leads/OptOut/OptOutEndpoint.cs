using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.Domain.Privacy;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Privacy.Interfaces;

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
        IConsentAuditService consentAudit,
        IComplianceConsentWriter complianceWriter,
        IOptions<ConsentHmacOptions> consentHmacOptions,
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
            return Results.Ok(new { status = "opted_out" });
        }

        // Idempotent: update frontmatter regardless of current opt-in state.
        await leadStore.UpdateMarketingOptInAsync(agentId, lead.Id, false, ct);

        var consent = new MarketingConsent
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
            Action = ConsentAction.OptOut,
            Source = ConsentSource.EmailLink,
        };
        await consentLog.RecordConsentAsync(agentId, consent, ct);

        // Triple-write: agent Drive (existing) + compliance Drive + Azure Table
        var hmacSignature = MarketingConsentLog.ComputeHmacSignature(consent, consentHmacOptions.Value.Secret);
        // Layer 1: Agent Drive CSV (already existing call above)
        await complianceWriter.WriteAsync(agentId, consent, hmacSignature, ct);  // Layer 2: RE* service-account Drive
        await consentAudit.RecordAsync(agentId, consent, hmacSignature, ct);     // Layer 3: Azure Table
        LeadDiagnostics.ConsentRecorded.Add(1);

        return Results.Ok(new { status = "opted_out" });
    }
}
