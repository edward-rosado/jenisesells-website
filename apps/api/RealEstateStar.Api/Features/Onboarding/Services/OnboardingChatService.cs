using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class OnboardingChatService(
    OnboardingStateMachine stateMachine,
    ILogger<OnboardingChatService> logger)
{
    public async IAsyncEnumerable<string> StreamResponseAsync(
        OnboardingSession session,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var tools = stateMachine.GetAllowedTools(session.CurrentState);
        var systemPrompt = BuildSystemPrompt(session);

        logger.LogInformation(
            "Streaming response for session {SessionId} in state {State} with {ToolCount} tools",
            session.Id, session.CurrentState, tools.Length);

        // TODO: Call Claude API with streaming, yield chunks.
        // For now, yield a stub response.
        await Task.CompletedTask;
        yield return $"[Stub] I'm in the {session.CurrentState} state. ";
        yield return $"Available tools: {string.Join(", ", tools)}. ";
        yield return $"You said: {userMessage}";
    }

    private static string BuildSystemPrompt(OnboardingSession session) =>
        $"""
        You are an onboarding assistant for Real Estate Star.
        Current state: {session.CurrentState}
        {(session.Profile is not null ? $"Agent: {session.Profile.Name}, {session.Profile.Brokerage}" : "No profile scraped yet.")}
        Guide the agent through onboarding step by step.
        """;
}
