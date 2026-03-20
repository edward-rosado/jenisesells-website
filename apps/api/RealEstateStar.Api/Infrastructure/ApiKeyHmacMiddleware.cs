using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RealEstateStar.Api.Infrastructure;

public class ApiKeyHmacMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyHmacOptions _options;
    private readonly ILogger<ApiKeyHmacMiddleware> _logger;

    public ApiKeyHmacMiddleware(RequestDelegate next, IOptions<ApiKeyHmacOptions> options, ILogger<ApiKeyHmacMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to lead submission endpoints
        if (!context.Request.Path.StartsWithSegments("/agents") || !context.Request.Path.Value!.Contains("/leads"))
        {
            await _next(context);
            return;
        }

        // Step 1: Validate API key header presence
        var apiKeyHeader = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKeyHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Step 2: Look up API key in dictionary
        if (!_options.ApiKeys.TryGetValue(apiKeyHeader, out var mappedAgentId))
        {
            _logger.LogWarning("[LEAD-019] Invalid API key presented");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Step 3: Validate that the mapped agentId matches the route parameter
        var routeAgentId = context.Request.RouteValues["agentId"]?.ToString();
        if (routeAgentId is null || !string.Equals(mappedAgentId, routeAgentId, StringComparison.Ordinal))
        {
            _logger.LogWarning("[LEAD-020] API key agentId {MappedAgentId} does not match route agentId {RouteAgentId}", mappedAgentId, routeAgentId);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Step 4: Validate timestamp header
        var timestampHeader = context.Request.Headers["X-Timestamp"].FirstOrDefault();
        if (!long.TryParse(timestampHeader, out var timestampSeconds))
        {
            _logger.LogWarning("[LEAD-022] Missing or unparseable X-Timestamp header");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var drift = Math.Abs(now - timestampSeconds);
        if (drift > _options.MaxTimestampDriftSeconds)
        {
            _logger.LogWarning("[LEAD-022] Timestamp drift {DriftSeconds}s exceeds maximum {MaxSeconds}s", drift, _options.MaxTimestampDriftSeconds);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Step 5: Validate HMAC signature — enable buffering so body can be read twice
        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
        }
        context.Request.Body.Position = 0;

        var signatureHeader = context.Request.Headers["X-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.Ordinal))
        {
            _logger.LogWarning("[LEAD-021] Missing or malformed X-Signature header");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var providedHex = signatureHeader["sha256=".Length..];
        var expectedSignature = ComputeHmac($"{_options.HmacSecret}:{mappedAgentId}", timestampHeader!, body);
        var expectedHex = expectedSignature["sha256=".Length..];

        if (!ConstantTimeEquals(providedHex, expectedHex))
        {
            _logger.LogWarning("[LEAD-021] HMAC signature mismatch");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    internal static string ComputeHmac(string secret, string timestamp, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var message = $"{timestamp}.{body}";
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
