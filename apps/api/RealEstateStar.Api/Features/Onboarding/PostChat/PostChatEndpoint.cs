using System.Threading.RateLimiting;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.PostChat;

public class PostChatEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/onboard/{sessionId}/chat", Handle)
            .RequireRateLimiting("chat-message");
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        PostChatRequest request,
        HttpContext httpContext,
        ISessionStore sessionStore,
        OnboardingChatService chatService,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null) return Results.NotFound();

        if (!ValidateBearerToken(httpContext, session))
            return Results.Unauthorized();

        // User message is added by StreamResponseAsync via BuildMessages — not here,
        // to avoid sending it to Claude twice. We persist after streaming completes.

        return Results.Stream(async stream =>
        {
            var writer = new StreamWriter(stream) { AutoFlush = true };

            await foreach (var chunk in chatService.StreamResponseAsync(session, request.Message, ct))
            {
                await writer.WriteAsync($"data: {chunk}\n\n");
            }

            await writer.WriteAsync("data: [DONE]\n\n");
            await sessionStore.SaveAsync(session, ct);
        }, "text/event-stream");
    }

    internal static bool ValidateBearerToken(HttpContext httpContext, OnboardingSession session)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var token = authHeader["Bearer ".Length..];
        return string.Equals(token, session.BearerToken, StringComparison.Ordinal);
    }
}
