using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Features.OAuth.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.OAuth.GenerateLink;

public class GenerateAuthLinkEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/internal/oauth/generate-link", Handle)
            .RequireRateLimiting("oauth-link-generate");

    internal static async Task<IResult> Handle(
        [FromBody] GenerateAuthLinkRequest request,
        HttpContext context,
        IConfiguration configuration,
        AuthorizationLinkService authorizationLinkService,
        ILogger<GenerateAuthLinkEndpoint> logger,
        CancellationToken ct)
    {
        // CRIT-2: Bearer token guard — required in production, optional (no-op) in dev if not configured
        var adminToken = configuration["OAuthLink:AdminToken"];
        if (!string.IsNullOrEmpty(adminToken))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var token = authHeader.StartsWith("Bearer ", StringComparison.Ordinal)
                ? authHeader["Bearer ".Length..]
                : string.Empty;
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(token),
                    Encoding.UTF8.GetBytes(adminToken)))
            {
                logger.LogWarning("[OAUTH-LINK-090] Unauthorized generate-link attempt from {Ip}",
                    context.Connection.RemoteIpAddress);
                return Results.Unauthorized();
            }
        }

        var validationErrors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationErrors, true))
        {
            return Results.ValidationProblem(
                validationErrors
                    .GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
                    .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage!).ToArray()));
        }

        var url = authorizationLinkService.GenerateLink(request.AccountId, request.AgentId, request.Email);

        // Parse expiry from the generated URL for the response
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var exp = long.Parse(query["exp"]!);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
        var expiresIn = (long)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds;

        logger.LogInformation(
            "[OAUTH-LINK-100] Authorization link generated. AccountId={AccountId}, AgentId={AgentId}, ExpiresAt={ExpiresAt}",
            request.AccountId, request.AgentId, expiresAt);

        return Results.Ok(new GenerateAuthLinkResponse(url, expiresAt, expiresIn));
    }
}

public sealed record GenerateAuthLinkRequest(
    [property: Required, MinLength(1)] string AccountId,
    [property: Required, MinLength(1)] string AgentId,
    [property: Required, EmailAddress] string Email);

public sealed record GenerateAuthLinkResponse(
    string AuthorizationUrl,
    DateTimeOffset ExpiresAt,
    long ExpiresIn);
