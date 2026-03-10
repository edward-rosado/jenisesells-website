using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.PostChat;
using RealEstateStar.Api.Features.Onboarding.Services;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.PostChat;

public class PostChatEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();

    private static OnboardingChatService CreateStubChatService() =>
        new(
            new HttpClient(),
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([]),
            NullLogger<OnboardingChatService>.Instance);

    [Fact]
    public async Task Handle_InvalidSession_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var request = new PostChatRequest { Message = "hello" };
        var result = await PostChatEndpoint.Handle(
            "nope", request, _mockStore.Object, CreateStubChatService(), CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Handle_ValidSession_AddsUserMessage()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new PostChatRequest { Message = "hello" };
        var result = await PostChatEndpoint.Handle(
            session.Id, request, _mockStore.Object, CreateStubChatService(), CancellationToken.None);

        // The endpoint adds the user message before streaming
        Assert.Equal(1, session.Messages.Count);
        Assert.Equal("user", session.Messages[0].Role);
        Assert.Equal("hello", session.Messages[0].Content);
        // Result is a streaming response — can't easily assert content in unit test
        Assert.NotNull(result);
    }
}
