using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class ChatServiceTests
{
    private readonly OnboardingChatService _service = new(
        new OnboardingStateMachine(),
        NullLogger<OnboardingChatService>.Instance);

    [Fact]
    public async Task StreamResponseAsync_YieldsChunks()
    {
        var session = OnboardingSession.Create(null);
        var chunks = new List<string>();

        await foreach (var chunk in _service.StreamResponseAsync(session, "hello", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.True(chunks.Count > 0);
        Assert.Contains(chunks, c => c.Contains("ScrapeProfile"));
    }

    [Fact]
    public async Task StreamResponseAsync_IncludesUserMessage()
    {
        var session = OnboardingSession.Create(null);
        var chunks = new List<string>();

        await foreach (var chunk in _service.StreamResponseAsync(session, "test message", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Contains(chunks, c => c.Contains("test message"));
    }

    [Fact]
    public async Task StreamResponseAsync_ReflectsCurrentState()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        var chunks = new List<string>();

        await foreach (var chunk in _service.StreamResponseAsync(session, "hi", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Contains(chunks, c => c.Contains("CollectBranding"));
    }
}
