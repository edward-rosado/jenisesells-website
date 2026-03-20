using Serilog.Context;

namespace RealEstateStar.Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";
    public const string CorrelationIdKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var rawId = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsValidCorrelationId(rawId) ? rawId! : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        context.Items[CorrelationIdKey] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    internal static bool IsValidCorrelationId(string? id) =>
        id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
