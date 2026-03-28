using RealEstateStar.Api.Features.Onboarding.Services;
using System.Text.Json;
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
        ILogger<PostChatEndpoint> logger,
        CancellationToken ct)
    {
        logger.LogDebug("[CHAT-010] Loading session {SessionId}", sessionId);

        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null)
        {
            logger.LogWarning("[CHAT-011] Session {SessionId} not found", sessionId);
            return Results.NotFound();
        }

        if (!ValidateBearerToken(httpContext, session))
        {
            logger.LogWarning("[CHAT-012] Bearer token validation failed for session {SessionId}", sessionId);
            return Results.Unauthorized();
        }

        logger.LogInformation("[CHAT-013] Starting stream for session {SessionId}, state={State}, message length={Len}",
            sessionId, session.CurrentState, request.Message?.Length ?? 0);

        // User message is added by StreamResponseAsync via BuildMessages — not here,
        // to avoid sending it to Claude twice. We persist after streaming completes.

        return Results.Stream(async stream =>
        {
            try
            {
                // Do NOT use AutoFlush = true — it calls synchronous Flush() which
                // Kestrel rejects. Call FlushAsync() manually instead.
                var writer = new StreamWriter(stream);

                await foreach (var chunk in chatService.StreamResponseAsync(session, request.Message, ct))
                {
                    // Card markers get their own event type so the frontend doesn't
                    // concatenate them into the text stream.
                    if (chunk.StartsWith("[CARD:"))
                    {
                        await writer.WriteAsync($"event: card\ndata: {chunk}\n\n");
                    }
                    else
                    {
                        // SSE uses \n as frame delimiter — newlines inside data break parsing.
                        // JSON-encode the chunk so \n becomes \\n, preserving formatting.
                        var safeChunk = JsonSerializer.Serialize(chunk);
                        await writer.WriteAsync($"data: {safeChunk}\n\n");
                    }
                    await writer.FlushAsync();
                }

                await writer.WriteAsync("data: [DONE]\n\n");
                await writer.FlushAsync();
                await sessionStore.SaveAsync(session, ct);

                logger.LogInformation("[CHAT-014] Stream completed successfully for session {SessionId}", sessionId);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[CHAT-015] Stream cancelled by client for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                // CRITICAL: Results.Stream() swallows exceptions — ASP.NET cannot send an error
                // status code because the 200 response has already started. We MUST log here
                // or the error is silently lost.
                logger.LogError(ex, "[CHAT-016] Stream failed for session {SessionId}. " +
                    "ExType={ExType}, Message={ExMessage}",
                    sessionId, ex.GetType().Name, ex.Message);

                // Save session state even on error — tool executions may have mutated
                // session state (e.g., state transitions, profile updates) before the failure
                try
                {
                    await sessionStore.SaveAsync(session, ct);
                    logger.LogInformation("[CHAT-018] Saved session {SessionId} after stream error", sessionId);
                }
                catch (Exception saveEx)
                {
                    logger.LogError(saveEx, "[CHAT-019] Failed to save session {SessionId} after stream error", sessionId);
                }

                // Attempt to send an error event to the client before the stream dies
                try
                {
                    var errorWriter = new StreamWriter(stream);
                    await errorWriter.WriteAsync($"data: [ERROR] Internal error — check server logs for CHAT-016\n\n");
                    await errorWriter.FlushAsync();
                }
                catch (Exception innerEx)
                {
                    logger.LogWarning(innerEx, "[CHAT-017] Failed to write error event to stream for session {SessionId}", sessionId);
                }
            }
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
