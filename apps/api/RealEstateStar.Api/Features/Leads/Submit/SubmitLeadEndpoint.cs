using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Api.Middleware;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.Domain.Privacy;
using RealEstateStar.Domain.Privacy.Interfaces;
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
        IConsentAuditService consentAudit,
        IComplianceConsentWriter complianceWriter,
        IOptions<ConsentHmacOptions> consentHmacOptions,
        ILeadDeadLetterStore deadLetterStore,
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

        using var activity = LeadDiagnostics.ActivitySource.StartActivity("lead.submit");
        activity?.SetTag("lead.agent_id", agentId);

        // 1. Validate agentId exists
        logger.LogInformation("[LEAD-000] Looking up agent config for {AgentId}. Store type: {StoreType}", agentId, leadStore.GetType().Name);
        var agent = await accountConfig.GetAccountAsync(agentId, ct);
        if (agent is null)
        {
            logger.LogWarning("[LEAD-000] Agent {AgentId} not found — returning 404", agentId);
            return Results.NotFound();
        }
        logger.LogInformation("[LEAD-000] Agent {AgentId} found. Email: {AgentEmail}", agentId, agent.Agent?.Email ?? "(none)");

        // 2. Dedup — check if lead with same email already exists
        var existingLead = await leadStore.GetByEmailAsync(agentId, request.Email, ct);
        Lead lead;
        bool isUpdate;

        if (existingLead is not null)
        {
            // Update existing lead with new details (e.g., adding seller to a buyer lead)
            lead = existingLead;
            isUpdate = true;

            // Merge lead type: if they were Buyer and now submitting as Seller, upgrade to Both
            if (lead.LeadType != request.LeadType && request.LeadType is not LeadType.Both)
                lead.MergeType(LeadType.Both);

            // Merge seller/buyer details if newly provided
            var hasNewDetails = false;
            if (request.Seller is not null && lead.SellerDetails is null)
            {
                lead.MergeSellerDetails(LeadMappers.MapSellerDetails(request.Seller));
                hasNewDetails = true;
            }
            if (request.Buyer is not null && lead.BuyerDetails is null)
            {
                lead.MergeBuyerDetails(LeadMappers.MapBuyerDetails(request.Buyer));
                hasNewDetails = true;
            }

            // Only re-run pipeline if new data was added (new seller/buyer details or type change)
            // Don't reset status if nothing changed — avoids re-running enrichment/scraping
            if (!hasNewDetails && lead.LeadType == request.LeadType)
            {
                logger.LogInformation(
                    "[LEAD-001-DEDUP] Lead already exists with same data. LeadId: {LeadId}, Status: {Status}. Skipping re-enqueue.",
                    lead.Id, lead.Status);
                return Results.Accepted($"/agents/{agentId}/leads/{lead.Id}", new SubmitLeadResponse(lead.Id, "already-exists"));
            }

            logger.LogInformation(
                "[LEAD-001] Lead updated (dedup). LeadId: {LeadId}, AgentId: {AgentId}, Type: {LeadType}, NewDetails: {HasNewDetails}, Email: {EmailHash}",
                lead.Id, agentId, lead.LeadType, hasNewDetails, HashEmail(lead.Email));
        }
        else
        {
            lead = request.ToLead(agentId);
            isUpdate = false;

            logger.LogInformation(
                "[LEAD-001] New lead received. LeadId: {LeadId}, AgentId: {AgentId}, Type: {LeadType}, Email: {EmailHash}",
                lead.Id, agentId, lead.LeadType, HashEmail(lead.Email));
        }

        activity?.SetTag("lead.id", lead.Id.ToString());
        activity?.SetTag("lead.type", lead.LeadType.ToString());
        activity?.SetTag("lead.is_update", isUpdate.ToString());

        // 3. Save lead — dead letter if storage fails
        logger.LogInformation("[LEAD-001a] {Action} lead {LeadId} via {StoreType}...",
            isUpdate ? "Updating" : "Saving", lead.Id, leadStore.GetType().Name);
        try
        {
            await leadStore.SaveAsync(lead, ct);
            logger.LogInformation("[LEAD-001b] Lead {LeadId} saved successfully.", lead.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-001b-FAIL] Lead save failed for {LeadId}. Writing to dead letter.", lead.Id);
            await deadLetterStore.RecordAsync(lead, "save", ex.Message, ct);
        }

        // 4. Record marketing consent — dead letter if consent recording fails
        logger.LogInformation("[LEAD-001c] Recording consent for lead {LeadId}...", lead.Id);
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
        try
        {
            await consentLog.RecordConsentAsync(agentId, consent, ct);
            logger.LogInformation("[LEAD-001d] Consent recorded for lead {LeadId}.", lead.Id);

            // Triple-write: agent Drive (existing) + compliance Drive + Azure Table
            var hmacSignature = MarketingConsentLog.ComputeHmacSignature(consent, consentHmacOptions.Value.Secret);
            logger.LogInformation("[LEAD-001e] Triple-write consent. ComplianceWriter: {WriterType}, ConsentAudit: {AuditType}",
                complianceWriter.GetType().Name, consentAudit.GetType().Name);
            await complianceWriter.WriteAsync(agentId, consent, hmacSignature, ct);  // Layer 2: RE* service-account Drive
            await consentAudit.RecordAsync(agentId, consent, hmacSignature, ct);     // Layer 3: Azure Table
            LeadDiagnostics.ConsentRecorded.Add(1);
            logger.LogInformation("[LEAD-001f] All consent layers written for lead {LeadId}.", lead.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-001d-FAIL] Consent recording failed for lead {LeadId}. Writing to dead letter.", lead.Id);
            await deadLetterStore.RecordAsync(lead, "consent", ex.Message, ct);
        }

        // 5. Enqueue background processing (enrichment, notification, home search)
        var correlationId = httpContext.Items[CorrelationIdMiddleware.CorrelationIdKey]?.ToString() ?? Guid.NewGuid().ToString();
        activity?.SetTag("correlation.id", correlationId);
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
