using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Middleware;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.Domain.Privacy;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Workers.Leads;

namespace RealEstateStar.Api.Features.Leads.Submit;

public class SubmitLeadEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/agents/{agentId}/leads", Handle)
            .RequireRateLimiting("lead-create");

    internal static async Task<IResult> Handle(
        string agentId,
        [FromBody] SubmitLeadRequest request,
        IAccountConfigService accountConfig,
        ILeadStore leadStore,
        IMarketingConsentLog consentLog,
        LeadProcessingChannel processingChannel,
        HttpContext httpContext,
        ILogger<SubmitLeadEndpoint> logger,
        CancellationToken ct)
    {
        // Validate DataAnnotations on the request
        var requestValidation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), requestValidation, true))
            return Results.ValidationProblem(GroupValidationErrors(requestValidation));

        // Business rule: selling requires seller details
        if (request.LeadType is LeadType.Seller or LeadType.Both && request.Seller is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Seller"] = ["Seller details are required when LeadType is 'seller' or 'both'."]
            });
        }

        // Business rule: buying requires buyer details
        if (request.LeadType is LeadType.Buyer or LeadType.Both && request.Buyer is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Buyer"] = ["Buyer details are required when LeadType is 'buyer' or 'both'."]
            });
        }

        // 1. Validate agentId exists
        var agent = await accountConfig.GetAccountAsync(agentId, ct);
        if (agent is null) return Results.NotFound();

        // 2. Map request to domain
        var lead = request.ToLead(agentId);

        logger.LogInformation(
            "[LEAD-001] Lead received. LeadId: {LeadId}, AgentId: {AgentId}, Type: {LeadType}, Email: {EmailHash}",
            lead.Id, agentId, lead.LeadType, HashEmail(lead.Email));

        // 3. Save lead (must succeed before returning 202)
        await leadStore.SaveAsync(lead, ct);

        // 4. Record marketing consent (must succeed before returning 202)
        var consent = new MarketingConsent
        {
            LeadId = lead.Id,
            Email = lead.Email,
            FirstName = lead.FirstName,
            LastName = lead.LastName,
            OptedIn = request.MarketingConsent.OptedIn,
            Channels = request.MarketingConsent.Channels,
            ConsentText = request.MarketingConsent.ConsentText,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow,
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };
        await consentLog.RecordConsentAsync(agentId, consent, ct);

        // 5. Enqueue background processing (enrichment, notification, home search)
        var correlationId = httpContext.Items[CorrelationIdMiddleware.CorrelationIdKey]?.ToString() ?? Guid.NewGuid().ToString();
        var processingRequest = new LeadProcessingRequest(agentId, lead, correlationId);

        await processingChannel.Writer.WriteAsync(processingRequest, ct);
        LeadDiagnostics.LeadsReceived.Add(1);

        logger.LogInformation(
            "[LEAD-002] Lead {LeadId} saved and enqueued for background processing. CorrelationId: {CorrelationId}",
            lead.Id, correlationId);

        // 6. Return 202 Accepted immediately
        return Results.Accepted($"/agents/{agentId}/leads/{lead.Id}", new SubmitLeadResponse(lead.Id, "received"));
    }

    internal static Dictionary<string, string[]> GroupValidationErrors(List<ValidationResult> results) =>
        results.GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage!).ToArray());

    internal static string HashEmail(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(hash)[..12];
    }
}
