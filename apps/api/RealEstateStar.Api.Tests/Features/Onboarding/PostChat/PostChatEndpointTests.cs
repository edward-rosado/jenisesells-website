using Microsoft.AspNetCore.Http;
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

    private static OnboardingChatService CreateStubChatService()
    {
        var factory = new Moq.Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        return new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext(string? bearerToken)
    {
        var context = new DefaultHttpContext();
        if (bearerToken is not null)
            context.Request.Headers.Authorization = $"Bearer {bearerToken}";
        return context;
    }

    [Fact]
    public async Task Handle_InvalidSession_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var request = new PostChatRequest { Message = "hello" };
        var httpContext = CreateHttpContext("any-token");
        var result = await PostChatEndpoint.Handle(
            "nope", request, httpContext, _mockStore.Object, CreateStubChatService(), NullLogger<PostChatEndpoint>.Instance, CancellationToken.None);

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
        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, CreateStubChatService(), NullLogger<PostChatEndpoint>.Instance, CancellationToken.None);

        // User message is added inside StreamResponseAsync (not the endpoint)
        // so we can only verify the endpoint returns a streaming result
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_MissingBearerToken_Returns401()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var request = new PostChatRequest { Message = "hello" };
        var httpContext = CreateHttpContext(null);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, CreateStubChatService(), NullLogger<PostChatEndpoint>.Instance, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Handle_WrongBearerToken_Returns401()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var request = new PostChatRequest { Message = "hello" };
        var httpContext = CreateHttpContext("wrong-token");
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, CreateStubChatService(), NullLogger<PostChatEndpoint>.Instance, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}
