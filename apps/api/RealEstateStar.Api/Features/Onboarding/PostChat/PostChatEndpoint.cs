using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.PostChat;

// No response DTO — this endpoint streams SSE. The stub returns JSON for now;
// Task 18 (Claude chat service) replaces this with real SSE streaming.
public class PostChatEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/onboard/{sessionId}/chat", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        PostChatRequest request,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null) return Results.NotFound();

        session.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = request.Message,
        });

        // TODO: Wire OnboardingChatService here (Task 18).
        // For now, echo back a stub assistant message.
        session.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = $"[Stub] Received: {request.Message}",
        });

        await sessionStore.SaveAsync(session, ct);

        return Results.Ok(new { response = session.Messages[^1].Content });
    }
}
