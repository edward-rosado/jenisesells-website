using Microsoft.Extensions.Options;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Api.Features.Sites.GetState;

/// <summary>
/// GET /sites/{accountId}/state
///
/// Returns the current site state from Cloudflare KV.
/// Requires a valid preview session (cookie or X-Preview-Session header).
///
/// KV key: site-state:v1:{accountId}
/// Possible values: "pending_approval", "pending_billing", "live", etc.
/// Returns 404 if no state exists yet.
/// </summary>
public class GetStateEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/sites/{accountId}/state", Handle);

    internal static async Task<IResult> Handle(
        string accountId,
        ICloudflareKvClient kvClient,
        IPreviewSessionStore sessionStore,
        IOptions<SiteOptions> siteOptions,
        HttpContext httpContext,
        ILogger<GetStateEndpoint> logger,
        CancellationToken ct)
    {
        // Validate preview session
        var (session, error) = await Preview.PreviewSessionValidator.ValidateAsync(
            accountId, httpContext, sessionStore, logger, ct);
        if (error is not null)
            return error;

        var namespaceId = siteOptions.Value.KvNamespaceId;
        var key = $"site-state:v1:{accountId}";

        string? raw;
        try
        {
            raw = await kvClient.GetAsync(namespaceId, key, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SITE-011] KV read failed. accountId={AccountId} key={Key}", accountId, key);
            return Results.Problem("Failed to read site state.", statusCode: 500);
        }

        if (raw is null)
        {
            logger.LogInformation("[SITE-010] Site state not found. accountId={AccountId}", accountId);
            return Results.NotFound(new { error = "Site state not found." });
        }

        // Strip surrounding quotes from the JSON string value (KV stores as "\"pending_approval\"")
        var state = raw.Trim('"');

        logger.LogInformation("[SITE-010] Site state retrieved. accountId={AccountId} state={State}",
            accountId, state);

        return Results.Ok(new GetStateResponse(accountId, state));
    }
}

public sealed record GetStateResponse(string AccountId, string State);
