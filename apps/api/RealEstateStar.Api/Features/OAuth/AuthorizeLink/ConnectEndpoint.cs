using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Features.OAuth.Services;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.OAuth.AuthorizeLink;

/// <summary>
/// POST /oauth/google/authorize/connect — re-validates HMAC, generates CSRF nonce, redirects to Google.
/// </summary>
public class ConnectEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/oauth/google/authorize/connect", Handle)
            .RequireRateLimiting("oauth-link-authorize")
            .DisableAntiforgery();

    internal static Task<IResult> Handle(
        [FromForm] ConnectRequest request,
        AuthorizationLinkService authorizationLinkService,
        GoogleOAuthService googleOAuthService,
        IConfiguration configuration,
        ILogger<ConnectEndpoint> logger,
        CancellationToken ct)
    {
        if (!authorizationLinkService.ValidateSignature(
                request.AccountId, request.AgentId, request.Email, request.Exp, request.Sig))
        {
            logger.LogWarning("[OAUTH-LINK-300] Invalid signature on connect POST. AccountId={AccountId}, AgentId={AgentId}",
                request.AccountId, request.AgentId);
            return Task.FromResult(Results.Unauthorized());
        }

        var nonce = authorizationLinkService.GenerateNonce(request.AccountId, request.AgentId, request.Email);
        // State is the nonce itself — avoids colon delimiter injection; identity is bound inside the nonce via AuthorizationLinkState
        var state = nonce;

        var activationRedirectUri = configuration["Google:AuthorizeLinkRedirectUri"]
            ?? configuration["Api:BaseUrl"]?.TrimEnd('/') + "/oauth/google/authorize/callback"
            ?? throw new InvalidOperationException("Google:AuthorizeLinkRedirectUri or Api:BaseUrl must be configured");

        var googleUrl = googleOAuthService.BuildActivationAuthorizationUrl(state, activationRedirectUri);

        logger.LogInformation("[OAUTH-LINK-301] Redirecting to Google OAuth. AccountId={AccountId}, AgentId={AgentId}",
            request.AccountId, request.AgentId);

        return Task.FromResult(Results.Redirect(googleUrl));
    }
}

public sealed record ConnectRequest(
    [property: Required, MinLength(1)] string AccountId,
    [property: Required, MinLength(1)] string AgentId,
    [property: Required, EmailAddress] string Email,
    long Exp,
    [property: Required, MinLength(1)] string Sig);
