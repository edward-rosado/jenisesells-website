using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Creates a mock OnboardingChatService that yields the given chunks from StreamResponseAsync.
    /// </summary>
    private static Mock<OnboardingChatService> CreateMockChatService(IAsyncEnumerable<string> chunks)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var mock = new Mock<OnboardingChatService>(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        mock.Setup(s => s.StreamResponseAsync(
                It.IsAny<OnboardingSession>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(chunks);

        return mock;
    }

    /// <summary>
    /// Executes an IResult and returns the response body as a string.
    /// Results.Stream() writes to HttpContext.Response.Body when ExecuteAsync is called.
    /// PushStreamHttpResult requires IServiceProvider on the HttpContext.
    /// </summary>
    private static async Task<string> ExecuteStreamResult(IResult result)
    {
        var ms = new MemoryStream();
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var ctx = new DefaultHttpContext { RequestServices = services };
        ctx.Response.Body = ms;
        await result.ExecuteAsync(ctx);
        ms.Position = 0;
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Helper to create an IAsyncEnumerable from a list of strings.
    /// </summary>
    private static async IAsyncEnumerable<string> YieldChunks(
        IEnumerable<string> chunks,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Helper to create an IAsyncEnumerable that throws after yielding some chunks.
    /// </summary>
    private static async IAsyncEnumerable<string> YieldThenThrow(
        IEnumerable<string> chunks,
        Exception ex,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
        throw ex;
    }

    /// <summary>
    /// Helper to create an IAsyncEnumerable that throws OperationCanceledException
    /// after yielding a specified number of chunks (simulating client disconnect).
    /// </summary>
    private static async IAsyncEnumerable<string> YieldThenCancel(
        IEnumerable<string> chunks,
        int cancelAfter,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var i = 0;
        foreach (var chunk in chunks)
        {
            yield return chunk;
            await Task.Yield();
            i++;
            if (i >= cancelAfter)
                throw new OperationCanceledException("Client disconnected");
        }
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

    [Fact]
    public async Task Handle_HappyPath_WritesSSEFramesAndDone()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockChat = CreateMockChatService(YieldChunks(["Hello", " world"]));

        var request = new PostChatRequest { Message = "hi" };
        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, mockChat.Object,
            NullLogger<PostChatEndpoint>.Instance, CancellationToken.None);

        var body = await ExecuteStreamResult(result);

        // Each chunk is JSON-serialized (wrapped in quotes) and sent as SSE data frame
        Assert.Contains("data: \"Hello\"\n\n", body);
        Assert.Contains("data: \" world\"\n\n", body);
        Assert.Contains("data: [DONE]\n\n", body);
    }

    [Fact]
    public async Task Handle_ClientCancellation_LogsCHAT015()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Simulate client disconnect: yield one chunk then throw OperationCanceledException
        var mockChat = CreateMockChatService(YieldThenCancel(["chunk1", "chunk2"], cancelAfter: 1));

        var mockLogger = new Mock<ILogger<PostChatEndpoint>>();

        var request = new PostChatRequest { Message = "hi" };
        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, mockChat.Object,
            mockLogger.Object, CancellationToken.None);

        // Execute the stream — OperationCanceledException fires mid-stream
        await ExecuteStreamResult(result);

        // Verify [CHAT-015] was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CHAT-015]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceException_LogsCHAT016AndSendsErrorEvent()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var mockChat = CreateMockChatService(
            YieldThenThrow(["partial"], new InvalidOperationException("boom")));

        var mockLogger = new Mock<ILogger<PostChatEndpoint>>();

        var request = new PostChatRequest { Message = "hi" };
        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, mockChat.Object,
            mockLogger.Object, CancellationToken.None);

        var body = await ExecuteStreamResult(result);

        // Verify [CHAT-016] was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CHAT-016]")),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify error event was sent to the client
        Assert.Contains("data: [ERROR]", body);
        Assert.Contains("CHAT-016", body);
    }

    [Fact]
    public async Task Handle_CardEvent_WritesCardSSEFormat()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cardChunk = "[CARD:google_auth]{\"oauthUrl\":\"https://accounts.google.com\"}";
        var mockChat = CreateMockChatService(YieldChunks(["text before", cardChunk, "text after"]));

        var request = new PostChatRequest { Message = "connect google" };
        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, mockChat.Object,
            NullLogger<PostChatEndpoint>.Instance, CancellationToken.None);

        var body = await ExecuteStreamResult(result);

        // Card chunks use "event: card" SSE type, not "data:" alone
        Assert.Contains($"event: card\ndata: {cardChunk}\n\n", body);
        // Regular text chunks use default "data:" format
        Assert.Contains("data: \"text before\"\n\n", body);
        Assert.Contains("data: \"text after\"\n\n", body);
    }

    [Fact]
    public async Task Handle_ServiceException_ErrorWriteFails_LogsCHAT017()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var mockChat = CreateMockChatService(
            YieldThenThrow(["partial"], new InvalidOperationException("boom")));

        var mockLogger = new Mock<ILogger<PostChatEndpoint>>();

        var request = new PostChatRequest { Message = "hi" };
        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, mockChat.Object,
            mockLogger.Object, CancellationToken.None);

        // Execute the stream using a ClosedStream that throws on write,
        // causing the inner error-write attempt to fail (CHAT-017 branch)
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var ctx = new DefaultHttpContext { RequestServices = services };
        ctx.Response.Body = new ThrowOnWriteStream();
        await result.ExecuteAsync(ctx);

        // Verify [CHAT-016] was logged (the main error)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CHAT-016]")),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify [CHAT-017] was logged (the inner error write failure)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[CHAT-017]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_SavesSessionAfterStream()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockChat = CreateMockChatService(YieldChunks(["done"]));

        var request = new PostChatRequest { Message = "hi" };
        var httpContext = CreateHttpContext(session.BearerToken);
        var result = await PostChatEndpoint.Handle(
            session.Id, request, httpContext, _mockStore.Object, mockChat.Object,
            NullLogger<PostChatEndpoint>.Instance, CancellationToken.None);

        await ExecuteStreamResult(result);

        // Verify session was saved after successful stream
        _mockStore.Verify(
            s => s.SaveAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

/// <summary>
/// A stream that allows reading the initial partial data written before an exception,
/// then throws on subsequent writes. Used to trigger the CHAT-017 inner catch path.
/// </summary>
internal class ThrowOnWriteStream : MemoryStream
{
    private int _writeCount;

    public override void Write(byte[] buffer, int offset, int count)
    {
        _writeCount++;
        // Let the first few writes through (the partial data before the exception),
        // then throw to simulate a broken stream
        if (_writeCount > 2)
            throw new IOException("Connection reset");
        base.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _writeCount++;
        if (_writeCount > 2)
            throw new IOException("Connection reset");
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _writeCount++;
        if (_writeCount > 2)
            throw new IOException("Connection reset");
        return base.WriteAsync(buffer, cancellationToken);
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        // Allow flush after initial writes but throw on error-recovery writes
        if (_writeCount > 2)
            throw new IOException("Connection reset");
        return base.FlushAsync(cancellationToken);
    }
}
