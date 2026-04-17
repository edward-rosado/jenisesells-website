using Microsoft.Extensions.Options;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Api.Features.Sites.Approve;

/// <summary>
/// POST /sites/{accountId}/approve
///
/// Agent approves their generated site content. Transitions site state from
/// "pending_approval" to "pending_billing" and returns a stub checkout URL.
///
/// Requires a valid preview session (cookie or X-Preview-Session header) scoped
/// to the given accountId. Real Stripe integration comes in C4; for now a
/// placeholder URL is returned.
///
/// KV key updated: site-state:v1:{accountId} → "pending_billing"
/// </summary>
public class ApproveEndpoint : IEndpoint
{
    // Stub checkout URL — replaced with real Stripe session URL in C4
    internal const string StubCheckoutUrl = "https://checkout.stripe.com/stub/not-yet-configured";

    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/sites/{accountId}/approve", Handle);

    internal static async Task<IResult> Handle(
        string accountId,
        ICloudflareKvClient kvClient,
        IPreviewSessionStore sessionStore,
        IOptions<SiteOptions> siteOptions,
        HttpContext httpContext,
        ILogger<ApproveEndpoint> logger,
        CancellationToken ct)
    {
        // Validate preview session (also checks accountId scope)
        var (session, error) = await Preview.PreviewSessionValidator.ValidateAsync(
            accountId, httpContext, sessionStore, logger, ct);
        if (error is not null)
            return error;

        var namespaceId = siteOptions.Value.KvNamespaceId;
        var stateKey = $"site-state:v1:{accountId}";

        // Update site-state to pending_billing
        try
        {
            await kvClient.PutAsync(namespaceId, stateKey, "\"pending_billing\"", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SITE-002] KV write failed for site-state. accountId={AccountId}", accountId);
            return Results.Problem("Failed to update site state.", statusCode: 500);
        }

        logger.LogInformation("[SITE-001] Site approved. accountId={AccountId} sessionId={SessionId} → pending_billing",
            accountId, session!.SessionId);

        return Results.Ok(new ApproveResponse(StubCheckoutUrl));
    }
}

public sealed record ApproveResponse(string CheckoutUrl);
