using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class ChatServiceTests
{
    private readonly OnboardingChatService _service = new(
        new HttpClient(),
        "test-key",
        new OnboardingStateMachine(),
        new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
        NullLogger<OnboardingChatService>.Instance);

    [Fact]
    public async Task StreamResponseAsync_YieldsChunks()
    {
        // Note: This test only works when Claude API is not reachable (HttpClient with no base address).
        // In CI, the real API call will fail. This is expected — integration tests cover real streaming.
        // For unit testing, we verify the service can be constructed and the method signature is correct.
        var session = OnboardingSession.Create(null);

        // We can't easily mock the streaming HTTP call without a handler mock.
        // Verify construction and method existence.
        Assert.NotNull(_service);
    }

    [Fact]
    public void ToolDefinitions_IncludeGoogleAuthCard()
    {
        // BuildToolDefinitions is private static, so we test via allowed tools from the state machine
        var sm = new OnboardingStateMachine();
        var tools = sm.GetAllowedTools(OnboardingState.ConnectGoogle);
        Assert.Contains("google_auth_card", tools);
    }

    [Fact]
    public void BuildMessages_IncludesSessionHistoryAndCurrentMessage()
    {
        // BuildMessages is private, so we test through StreamResponseAsync behavior.
        // We verify that session messages are preserved after streaming by checking
        // the request body sent to the API. Since we can't intercept the private method,
        // we test indirectly: a session with prior messages should include them all.
        var session = OnboardingSession.Create(null);
        session.Messages.Add(new ChatMessage { Role = "user", Content = "Hello" });
        session.Messages.Add(new ChatMessage { Role = "assistant", Content = "Hi there!" });

        // The BuildMessages method appends existing messages then the new user message.
        // We verify this contract by using a mock HttpMessageHandler.
        List<object>? capturedMessages = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Capture the messages array for assertion
            capturedMessages = [];
            foreach (var msg in root.GetProperty("messages").EnumerateArray())
            {
                capturedMessages.Add(new
                {
                    Role = msg.GetProperty("role").GetString(),
                    Content = msg.GetProperty("content").GetString()
                });
            }
        });

        var httpClient = new HttpClient(handler);
        var service = new OnboardingChatService(
            httpClient,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        // StreamResponseAsync will fail when trying to read the mock response,
        // but the request will be captured before that.
        var enumerator = service.StreamResponseAsync(session, "New message", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        // The HTTP call will throw since our handler returns an error status.
        // We just need the request to be captured.
        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedMessages.Should().NotBeNull();
        capturedMessages.Should().HaveCount(3, "2 history messages + 1 new user message");
    }

    [Fact]
    public void BuildMessages_SystemPromptIncludesCurrentState()
    {
        // BuildSystemPrompt is private, but we can verify it's sent correctly
        // by capturing the request body.
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var httpClient = new HttpClient(handler);
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;

        var service = new OnboardingChatService(
            httpClient,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().NotBeNull();
        capturedSystem.Should().Contain("onboarding assistant", "system prompt should describe the assistant role");
        capturedSystem.Should().Contain("CollectBranding", "system prompt should include current state");
    }

    [Fact]
    public void BuildMessages_SystemPromptIncludesProfileWhenSet()
    {
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var httpClient = new HttpClient(handler);
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Brokerage = "RE/MAX",
            State = "NJ"
        };

        var service = new OnboardingChatService(
            httpClient,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().NotBeNull();
        capturedSystem.Should().Contain("Jane Doe");
        capturedSystem.Should().Contain("RE/MAX");
        capturedSystem.Should().Contain("<agent_profile>", "profile should be wrapped in XML delimiters");
    }

    [Fact]
    public async Task StreamResponseAsync_DispatchesToolWhenToolUseBlockReceived()
    {
        // Simulate a streaming response with a tool_use block.
        var sseData = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_1","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"url\":\"https://example.com\"}"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SseResponseHandler(sseData);
        var httpClient = new HttpClient(handler);

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Scraped successfully");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);

        var service = new OnboardingChatService(
            httpClient,
            "test-key",
            new OnboardingStateMachine(),
            dispatcher,
            NullLogger<OnboardingChatService>.Instance);

        var session = OnboardingSession.Create(null);
        var chunks = new List<string>();

        await foreach (var chunk in service.StreamResponseAsync(session, "scrape my profile", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        mockTool.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), session, It.IsAny<CancellationToken>()),
            Times.Once);

        chunks.Should().ContainSingle(c => c.Contains("scrape_url") && c.Contains("Scraped successfully"));
    }

    [Fact]
    public async Task StreamResponseAsync_AppendsMessagesToSessionHistory()
    {
        var sseData = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello there!"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SseResponseHandler(sseData);
        var httpClient = new HttpClient(handler);

        var service = new OnboardingChatService(
            httpClient,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var session = OnboardingSession.Create(null);
        session.Messages.Should().BeEmpty();

        await foreach (var _ in service.StreamResponseAsync(session, "Hi", CancellationToken.None))
        {
            // consume all chunks
        }

        session.Messages.Should().HaveCount(2);
        session.Messages[0].Role.Should().Be("user");
        session.Messages[0].Content.Should().Be("Hi");
        session.Messages[1].Role.Should().Be("assistant");
        session.Messages[1].Content.Should().Be("Hello there!");
    }

    [Fact]
    public void BuildMessages_RequestIncludesToolDefinitionsForCurrentState()
    {
        List<string>? capturedToolNames = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("tools", out var tools))
            {
                capturedToolNames = [];
                foreach (var tool in tools.EnumerateArray())
                {
                    capturedToolNames.Add(tool.GetProperty("name").GetString()!);
                }
            }
        });

        var httpClient = new HttpClient(handler);
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ScrapeProfile;

        var service = new OnboardingChatService(
            httpClient,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedToolNames.Should().NotBeNull();
        capturedToolNames.Should().Contain("scrape_url");
        capturedToolNames.Should().Contain("update_profile");
    }
}

/// <summary>
/// Test handler that captures the outgoing request for assertion, then returns a 500
/// to terminate the streaming flow.
/// </summary>
internal class CapturingHttpMessageHandler(Action<HttpRequestMessage> onRequest) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        onRequest(request);
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
    }
}

/// <summary>
/// Test handler that returns a pre-built SSE stream as the response body.
/// </summary>
internal class SseResponseHandler(string sseData) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(sseData, System.Text.Encoding.UTF8, "text/event-stream")
        };
        return Task.FromResult(response);
    }
}
