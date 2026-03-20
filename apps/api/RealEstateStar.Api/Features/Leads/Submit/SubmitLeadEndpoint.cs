using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Infrastructure;

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
        ILeadEnricher enricher,
        ILeadNotifier notifier,
        IHomeSearchProvider homeSearchProvider,
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
            Timestamp = DateTime.UtcNow
        };
        await consentLog.RecordConsentAsync(agentId, consent, ct);

        // 5. Return 202 Accepted immediately
        var response = new SubmitLeadResponse(lead.Id, "received");

        // 6. Fire-and-forget background pipeline
        _ = Task.Run(async () =>
        {
            // Enrich lead
            LeadEnrichment enrichment = LeadEnrichment.Empty();
            LeadScore score = LeadScore.Default("enrichment not yet run");
            try
            {
                (enrichment, score) = await enricher.EnrichAsync(lead, CancellationToken.None);
                await leadStore.UpdateEnrichmentAsync(agentId, lead.Id, enrichment, score, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[LEAD-007] Enrichment failed for lead {LeadId}", lead.Id);
            }

            // Notify agent
            try
            {
                await notifier.NotifyAgentAsync(agentId, lead, enrichment, score, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[LEAD-005] Notification failed for lead {LeadId}", lead.Id);
            }

            // Home search for buyers
            if (lead.LeadType is LeadType.Buyer or LeadType.Both && lead.BuyerDetails is not null)
            {
                try
                {
                    var criteria = new HomeSearchCriteria
                    {
                        Area = string.IsNullOrWhiteSpace(lead.BuyerDetails.State)
                            ? lead.BuyerDetails.City
                            : $"{lead.BuyerDetails.City}, {lead.BuyerDetails.State}",
                        MinPrice = lead.BuyerDetails.MinBudget,
                        MaxPrice = lead.BuyerDetails.MaxBudget,
                        MinBeds = lead.BuyerDetails.Bedrooms,
                        MinBaths = lead.BuyerDetails.Bathrooms
                    };
                    var listings = await homeSearchProvider.SearchAsync(criteria, CancellationToken.None);
                    if (listings.Count > 0)
                    {
                        // Store a synthetic search ID based on the lead so downstream can correlate
                        var searchId = $"search-{lead.Id}";
                        await leadStore.UpdateHomeSearchIdAsync(agentId, lead.Id, searchId, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[LEAD-006] Home search failed for lead {LeadId}", lead.Id);
                }
            }
        });

        return Results.Accepted($"/agents/{agentId}/leads/{lead.Id}", response);
    }

    internal static Dictionary<string, string[]> GroupValidationErrors(List<ValidationResult> results) =>
        results.GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage!).ToArray());
}
