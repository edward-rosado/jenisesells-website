using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Api.Features.Preview.ExchangeToken;

/// <summary>
/// POST /preview-sessions/exchange
///
/// Validates a short-lived HMAC-signed exchange token and creates a
/// preview session backed by Azure Table Storage. Sets an HttpOnly session
/// cookie so subsequent requests can present the session without re-exchanging.
///
/// Exchange token format (JSON, HMAC-SHA256 signed):
///   { "accountId": "...", "issuedAt": "ISO-8601 UTC", "nonce": "..." }
///   Header: Authorization: Bearer {base64(payload)}.{base64(signature)}
///
/// Single-use: the session insert uses Azure Table "add" semantics, which
/// fails with 409 if the sessionId is already present.
/// </summary>
public class ExchangeTokenEndpoint : IEndpoint
{
    internal const string SessionCookieName = PreviewSessionValidator.CookieName;
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(15);

    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/preview-sessions/exchange", Handle);

    internal static async Task<IResult> Handle(
        [FromBody] ExchangeTokenRequest request,
        IPreviewSessionStore sessionStore,
        IOptions<PreviewOptions> options,
        HttpContext httpContext,
        ILogger<ExchangeTokenEndpoint> logger,
        CancellationToken ct)
    {
        var hmacKey = options.Value.HmacKey;
        if (string.IsNullOrWhiteSpace(hmacKey))
        {
            logger.LogError("[PREVIEW-010] Preview:HmacKey is not configured");
            return Results.Problem("Preview service is not configured.", statusCode: 503);
        }

        // 1. Parse and validate the exchange token
        ExchangeTokenPayload? payload;
        try
        {
            payload = ParseAndValidateToken(request.Token, hmacKey, out var tokenError);
            if (payload is null)
            {
                logger.LogWarning("[PREVIEW-010] Invalid exchange token: {Reason}", tokenError);
                return Results.Problem(
                    detail: tokenError,
                    statusCode: 400,
                    title: "Invalid exchange token");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PREVIEW-010] Exchange token parse error");
            return Results.Problem(
                detail: "Malformed exchange token.",
                statusCode: 400,
                title: "Invalid exchange token");
        }

        // 2. Check 15-minute TTL
        var age = DateTime.UtcNow - payload.IssuedAt;
        if (age > TokenTtl || age < TimeSpan.Zero)
        {
            logger.LogWarning("[PREVIEW-011] Exchange token expired. issuedAt={IssuedAt} age={Age}",
                payload.IssuedAt, age);
            return Results.Problem(
                detail: "Exchange token has expired.",
                statusCode: 400,
                title: "Token expired");
        }

        // 3. Create session (sessionId = nonce = single-use handle from token)
        var sessionId = GenerateSessionId();
        var now = DateTime.UtcNow;
        var session = new PreviewSession(
            SessionId: sessionId,
            AccountId: payload.AccountId,
            ExpiresAt: now.AddHours(24),
            Revoked: false,
            RevokedAt: null);

        try
        {
            await sessionStore.CreateAsync(session, ct);
        }
        catch (InvalidOperationException)
        {
            // Exchange token already used — race condition or replay
            logger.LogWarning("[PREVIEW-010] Exchange token replay detected for accountId={AccountId}", payload.AccountId);
            return Results.Problem(
                detail: "Exchange token has already been used.",
                statusCode: 400,
                title: "Token already consumed");
        }

        // 4. Set HttpOnly session cookie
        httpContext.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(24)
        });

        logger.LogInformation("[PREVIEW-001] Preview session created. sessionId={SessionId} accountId={AccountId}",
            sessionId, payload.AccountId);

        return Results.Ok(new ExchangeTokenResponse(sessionId, payload.AccountId, session.ExpiresAt));
    }

    /// <summary>
    /// Parses and validates a signed exchange token of the form:
    ///   {base64url(payload)}.{base64url(HMAC-SHA256 signature)}
    /// Returns the payload if valid, or null with an error message.
    /// </summary>
    internal static ExchangeTokenPayload? ParseAndValidateToken(string token, string hmacKey, out string error)
    {
        error = "";
        var dot = token.LastIndexOf('.');
        if (dot < 0)
        {
            error = "Token format invalid — expected payload.signature";
            return null;
        }

        var payloadPart = token[..dot];
        var signaturePart = token[(dot + 1)..];

        // Verify HMAC-SHA256 signature
        var keyBytes = Encoding.UTF8.GetBytes(hmacKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadPart);
        var expectedSig = HMACSHA256.HashData(keyBytes, payloadBytes);
        byte[] actualSig;
        try
        {
            actualSig = Convert.FromBase64String(Base64UrlDecode(signaturePart));
        }
        catch
        {
            error = "Token signature encoding invalid";
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSig, actualSig))
        {
            error = "Token signature invalid";
            return null;
        }

        // Decode and deserialize payload
        string payloadJson;
        try
        {
            payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(Base64UrlDecode(payloadPart)));
        }
        catch
        {
            error = "Token payload encoding invalid";
            return null;
        }

        ExchangeTokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ExchangeTokenPayload>(payloadJson, JsonOpts);
        }
        catch
        {
            error = "Token payload JSON invalid";
            return null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccountId))
        {
            error = "Token payload missing required fields";
            return null;
        }

        return payload;
    }

    private static string GenerateSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>Converts base64url encoding to standard base64 with padding.</summary>
    private static string Base64UrlDecode(string base64url) =>
        base64url.Replace('-', '+').Replace('_', '/') +
        (base64url.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => ""
        };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed record ExchangeTokenRequest(string Token);
public sealed record ExchangeTokenResponse(string SessionId, string AccountId, DateTime ExpiresAt);

/// <summary>Deserialized payload from a signed exchange token.</summary>
public sealed class ExchangeTokenPayload
{
    public string AccountId { get; init; } = "";
    public DateTime IssuedAt { get; init; }
    public string Nonce { get; init; } = "";
}
