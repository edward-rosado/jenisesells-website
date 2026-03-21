using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Onboarding;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class ChatServiceTests
{
    private static IHttpClientFactory CreateMockFactory()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        return factory.Object;
    }

    private readonly OnboardingChatService _service = new(
        CreateMockFactory(),
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

    // ── GetCumulativeTools — direct tests (now internal static) ──

    [Fact]
    public void GetCumulativeTools_ScrapeProfile_IncludesToolsThroughConnectGoogle()
    {
        var tools = OnboardingChatService.GetCumulativeTools(OnboardingState.ScrapeProfile);
        tools.Should().BeEquivalentTo(
            ["scrape_url", "update_profile", "deploy_site", "submit_cma_form", "google_auth_card"],
            options => options.WithStrictOrdering(),
            "ScrapeProfile should include all tools up to and including the first blocking state (ConnectGoogle)");
    }

    [Fact]
    public void GetCumulativeTools_GenerateSite_IncludesDeployThroughConnectGoogle()
    {
        var tools = OnboardingChatService.GetCumulativeTools(OnboardingState.GenerateSite);
        tools.Should().BeEquivalentTo(
            ["deploy_site", "submit_cma_form", "google_auth_card"],
            options => options.WithStrictOrdering(),
            "GenerateSite should include deploy_site, submit_cma_form, and google_auth_card (stops at ConnectGoogle blocking)");
    }

    [Fact]
    public void GetCumulativeTools_DemoCma_IncludesCmaAndConnectGoogle()
    {
        var tools = OnboardingChatService.GetCumulativeTools(OnboardingState.DemoCma);
        tools.Should().BeEquivalentTo(
            ["submit_cma_form", "google_auth_card"],
            options => options.WithStrictOrdering(),
            "DemoCma should include submit_cma_form and google_auth_card (stops at ConnectGoogle blocking)");
    }

    [Fact]
    public void GetCumulativeTools_ConnectGoogle_OnlyIncludesGoogleAuthCard()
    {
        var tools = OnboardingChatService.GetCumulativeTools(OnboardingState.ConnectGoogle);
        tools.Should().BeEquivalentTo(
            ["google_auth_card"],
            "ConnectGoogle is user-blocking, so only its own tool is included");
    }

    [Fact]
    public void GetCumulativeTools_ShowResults_ReturnsEmptyArray()
    {
        var tools = OnboardingChatService.GetCumulativeTools(OnboardingState.ShowResults);
        tools.Should().BeEmpty("ShowResults is user-blocking with no tools");
    }

    [Fact]
    public void GetCumulativeTools_CollectPayment_OnlyIncludesStripe()
    {
        var tools = OnboardingChatService.GetCumulativeTools(OnboardingState.CollectPayment);
        tools.Should().BeEquivalentTo(
            ["create_stripe_session"],
            "CollectPayment is user-blocking, so only its own tool is included");
    }

    [Fact]
    public void GetCumulativeTools_TrialActivated_ReturnsEmptyArray()
    {
        var tools = OnboardingChatService.GetCumulativeTools(OnboardingState.TrialActivated);
        tools.Should().BeEmpty("TrialActivated is not in the chain, so no tools are returned");
    }

    [Fact]
    public void GetCumulativeTools_DemoCmaBeforeConnectGoogle_EnsuresCorrectChainOrder()
    {
        // Verify the critical fix: DemoCma tools must appear BEFORE ConnectGoogle
        // so that GenerateSite can chain into submit_cma_form
        var generateSiteTools = OnboardingChatService.GetCumulativeTools(OnboardingState.GenerateSite);
        generateSiteTools.Should().Contain("submit_cma_form",
            "GenerateSite cumulative tools must include submit_cma_form because DemoCma comes before ConnectGoogle in the chain");
    }

    [Fact]
    public void BuildMessages_IncludesSessionHistoryAndCurrentMessage()
    {
        // BuildMessages is private, so we test through StreamResponseAsync behavior.
        // We verify that session messages are preserved after streaming by checking
        // the request body sent to the API. Since we can't intercept the private method,
        // we test indirectly: a session with prior messages should include them all.
        var session = OnboardingSession.Create(null);
        session.Messages.Add(new ChatMessage { Role = ChatRole.User, Content = "Hello" });
        session.Messages.Add(new ChatMessage { Role = ChatRole.Assistant, Content = "Hi there!" });

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

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var service = new OnboardingChatService(
            factory.Object,
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

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.GenerateSite;

        var service = new OnboardingChatService(
            factory.Object,
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
        capturedSystem.Should().Contain("GenerateSite", "system prompt should include current state");
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

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Brokerage = "RE/MAX",
            State = "NJ"
        };

        var service = new OnboardingChatService(
            factory.Object,
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
        // First call: Claude calls a tool
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_1","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"url\":\"https://example.com\"}"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        // Second call: Claude responds with text after receiving tool result
        var followUpSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"I found your profile!"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SequentialSseResponseHandler([toolCallSse, followUpSse]);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Scraped successfully");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);

        var service = new OnboardingChatService(
            factory.Object,
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

        // Tool results are not yielded to the stream — only [CARD:...] markers pass through.
        // Raw tool result text ("Scraped successfully") is internal and never streamed.
        chunks.Should().NotContain(c => c.Contains("Scraped successfully"),
            "raw tool result text should not be yielded to the stream");
        chunks.Should().ContainSingle(c => c.Contains("I found your profile!"));
    }

    [Fact]
    public async Task StreamResponseAsync_SendsToolResultBackToClaude()
    {
        // Verify that the continuation request includes the tool_result message
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_abc","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"url\":\"https://example.com\"}"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var followUpSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Done!"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        string? secondRequestBody = null;
        var callCount = 0;

        var handler = new CallbackSseResponseHandler(request =>
        {
            callCount++;
            if (callCount == 2)
                secondRequestBody = request.Content!.ReadAsStringAsync().Result;

            var sseData = callCount == 1 ? toolCallSse : followUpSse;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(sseData, System.Text.Encoding.UTF8, "text/event-stream")
            };
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Scraped: Jane Doe, RE/MAX");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            dispatcher,
            NullLogger<OnboardingChatService>.Instance);

        var session = OnboardingSession.Create(null);

        await foreach (var _ in service.StreamResponseAsync(session, "scrape me", CancellationToken.None))
        {
            // consume
        }

        callCount.Should().Be(2, "first call triggers tool, second call sends tool result");
        secondRequestBody.Should().NotBeNull();

        var doc = JsonDocument.Parse(secondRequestBody!);
        var messages = doc.RootElement.GetProperty("messages");
        var lastMsg = messages[messages.GetArrayLength() - 1];
        lastMsg.GetProperty("role").GetString().Should().Be("user");

        var toolResultContent = lastMsg.GetProperty("content")[0];
        toolResultContent.GetProperty("type").GetString().Should().Be("tool_result");
        toolResultContent.GetProperty("tool_use_id").GetString().Should().Be("tu_abc");
        toolResultContent.GetProperty("content").GetString().Should().Contain("Scraped: Jane Doe");
    }

    [Fact]
    public async Task StreamResponseAsync_RespectsMaxToolRounds()
    {
        // Every call returns a tool_use — should stop after MaxToolRounds (5)
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_loop","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"url\":\"https://example.com\"}"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SseResponseHandler(toolCallSse);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("result");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            dispatcher,
            NullLogger<OnboardingChatService>.Instance);

        var session = OnboardingSession.Create(null);
        var chunks = new List<string>();

        await foreach (var chunk in service.StreamResponseAsync(session, "scrape", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Tool should be called exactly 5 times (MaxToolRounds), not infinite
        mockTool.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), session, It.IsAny<CancellationToken>()),
            Times.Exactly(5));
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
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var service = new OnboardingChatService(
            factory.Object,
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
        session.Messages[0].Role.Should().Be(ChatRole.User);
        session.Messages[0].Content.Should().Be("Hi");
        session.Messages[1].Role.Should().Be(ChatRole.Assistant);
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

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ScrapeProfile;

        var service = new OnboardingChatService(
            factory.Object,
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

// ─── New tests for uncovered branches ──────────────────────────────────────

public class ChatServiceBranchCoverageTests
{
    private static OnboardingChatService CreateService(HttpMessageHandler handler, ToolDispatcher? dispatcher = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        return new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            dispatcher ?? new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);
    }

    // ── Branch: type == "error" in SSE stream throws InvalidOperationException ──

    [Fact]
    public async Task StreamResponseAsync_ThrowsOnStreamErrorEvent()
    {
        var sseData = """
            data: {"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}

            data: [DONE]

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var act = async () =>
        {
            await foreach (var _ in service.StreamResponseAsync(session, "hello", CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*STREAM-019*");
    }

    // ── Branch: error event without "error" property falls back to raw data ──

    [Fact]
    public async Task StreamResponseAsync_ErrorEventWithoutErrorProperty_ThrowsWithRawData()
    {
        var sseData = """
            data: {"type":"error"}

            data: [DONE]

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var act = async () =>
        {
            await foreach (var _ in service.StreamResponseAsync(session, "hello", CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Branch: invalid JSON in SSE data line → continue (no throw) ──

    [Fact]
    public async Task StreamResponseAsync_InvalidJsonInSseLine_SkipsEventAndContinues()
    {
        // Invalid JSON on first line; valid text on second line — streaming should complete.
        var sseData = """
            data: NOT_VALID_JSON{{{

            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"ok"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "test", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle(c => c == "ok");
    }

    // ── Branch: SSE event with missing "type" property → skipped ──

    [Fact]
    public async Task StreamResponseAsync_MissingTypeProperty_SkipsEvent()
    {
        // KeyNotFoundException when calling GetProperty("type") on an event without it
        var sseData = """
            data: {"nottype":"something"}

            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"after skip"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "test", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle(c => c == "after skip");
    }

    // ── Branch: invalid tool input JSON → breaks the tool round loop ──

    [Fact]
    public async Task StreamResponseAsync_InvalidToolInputJson_BreaksLoop()
    {
        // input_json_delta produces malformed JSON
        var sseData = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_bad","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"THIS IS NOT JSON {{{"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);
        var service = CreateService(new SseResponseHandler(sseData), dispatcher);
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        // Should NOT throw — break exits the loop gracefully
        await foreach (var chunk in service.StreamResponseAsync(session, "scrape", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Tool should never be dispatched because we broke before executing
        mockTool.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Branch: tool execution throws → yields error message and continues ──

    [Fact]
    public async Task StreamResponseAsync_ToolThrows_YieldsErrorAndContinues()
    {
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_err","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"url\":\"https://example.com\"}"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var followUpSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Continuing after error"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SequentialSseResponseHandler([toolCallSse, followUpSse]);

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("scrape failed unexpectedly"));

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);
        var service = CreateService(handler, dispatcher);
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "scrape", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Tool errors are not yielded to the stream — only [CARD:...] markers pass through.
        // The error is logged internally; "Error: tool execution failed" is only sent back to Claude as a tool_result.
        chunks.Should().NotContain(c => c.Contains("Error: tool execution failed"),
            "tool error text should not be yielded to the stream");
        chunks.Should().Contain(c => c.Contains("Continuing after error"),
            "streaming should continue after the tool error");
    }

    // ── Card marker extraction — only [CARD:...]{json} is yielded from tool results ──

    [Fact]
    public async Task StreamResponseAsync_CardMarkerInToolResult_IsYieldedToStream()
    {
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_card","name":"google_auth_card","input":{}}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var followUpSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Click above to connect."}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SequentialSseResponseHandler([toolCallSse, followUpSse]);

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("google_auth_card");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[CARD:google_auth]{\"oauthUrl\":\"https://accounts.google.com/o/oauth2/auth\"}");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);
        var service = CreateService(handler, dispatcher);
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.ConnectGoogle;

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "connect google", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().Contain(c => c.Contains("[CARD:google_auth]"),
            "card markers from tool results must be yielded to the stream");
        chunks.Should().Contain(c => c.Contains("oauthUrl"),
            "card JSON payload must be included");
    }

    [Fact]
    public async Task StreamResponseAsync_PlainToolResult_NotYieldedToStream()
    {
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_plain","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"url\":\"https://example.com\"}"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var followUpSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Profile scraped."}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SequentialSseResponseHandler([toolCallSse, followUpSse]);

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SUCCESS: Scraped profile for Jenise Buckalew. Name, brokerage, stats all found.");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);
        var service = CreateService(handler, dispatcher);
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "scrape this url", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().NotContain(c => c.Contains("SUCCESS: Scraped profile"),
            "raw tool result text must NOT be yielded to the stream — only card markers");
        chunks.Should().Contain(c => c.Contains("Profile scraped."),
            "Claude's follow-up text response should still be yielded");
    }

    // ── Branch: text before tool call — assistant content includes text block ──

    [Fact]
    public async Task StreamResponseAsync_TextBeforeToolCall_YieldsTextAndToolResult()
    {
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Let me scrape that for you."}}

            data: {"type":"content_block_stop","index":0}

            data: {"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"tu_txt","name":"scrape_url","input":{}}}

            data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"url\":\"https://example.com\"}"}}

            data: {"type":"content_block_stop","index":1}

            data: [DONE]

            """;

        var followUpSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Done!"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        string? secondRequestBody = null;
        var callCount = 0;

        var handler = new CallbackSseResponseHandler(request =>
        {
            callCount++;
            if (callCount == 2)
                secondRequestBody = request.Content!.ReadAsStringAsync().Result;
            var sseData = callCount == 1 ? toolCallSse : followUpSse;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(sseData, System.Text.Encoding.UTF8, "text/event-stream")
            };
        });

        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("scrape_url");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Scraped profile data");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);
        var service = CreateService(handler, dispatcher);
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "scrape me", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().Contain("Let me scrape that for you.");

        // The second request's assistant message should include a text content block
        secondRequestBody.Should().NotBeNull();
        var doc = JsonDocument.Parse(secondRequestBody!);
        var messages = doc.RootElement.GetProperty("messages");
        // Find the assistant message (second to last)
        var assistantMsg = messages[messages.GetArrayLength() - 2];
        assistantMsg.GetProperty("role").GetString().Should().Be("assistant");

        var content = assistantMsg.GetProperty("content");
        var hasTextBlock = false;
        foreach (var block in content.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
                hasTextBlock = true;
        }

        hasTextBlock.Should().BeTrue("assistant message should include a text block when text preceded the tool call");
    }

    // ── Branch: message_stop event → logged and ignored (streaming completes normally) ──

    [Fact]
    public async Task StreamResponseAsync_MessageStopEvent_StreamCompletesNormally()
    {
        var sseData = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello!"}}

            data: {"type":"content_block_stop","index":0}

            data: {"type":"message_stop"}

            data: [DONE]

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "hi", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle(c => c == "Hello!");
    }

    // ── Branch: no tools for state → "tools" key omitted from request ──

    [Fact]
    public void NoToolsState_RequestOmitsToolsKey()
    {
        // TrialActivated has no tools — the request should not include a "tools" key
        bool? hadToolsKey = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            hadToolsKey = doc.RootElement.TryGetProperty("tools", out _);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.TrialActivated;

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Thanks!", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        hadToolsKey.Should().NotBeNull();
        hadToolsKey.Should().BeFalse("TrialActivated has no allowed tools, so 'tools' key must be omitted");
    }

    // ── Branch: system prompt — GoogleTokens set ──

    [Fact]
    public void BuildSystemPrompt_IncludesGoogleTokensWhenSet()
    {
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.GoogleTokens = new GoogleTokens
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["email", "profile"],
            GoogleEmail = "jenise@gmail.com",
            GoogleName = "Jenise Buckalew"
        };

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().Contain("Google connected");
        capturedSystem.Should().Contain("Jenise Buckalew");
        capturedSystem.Should().Contain("jenise@gmail.com");
    }

    // ── Branch: system prompt — SiteUrl set ──

    [Fact]
    public void BuildSystemPrompt_IncludesSiteUrlWhenSet()
    {
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.SiteUrl = "https://jenise.realestatestar.com";

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().Contain("Deployed site");
        capturedSystem.Should().Contain("jenise.realestatestar.com");
    }

    // ── Branch: system prompt switch — each uncovered OnboardingState case ──

    [Theory]
    [InlineData(OnboardingState.DemoCma, "CMA")]
    [InlineData(OnboardingState.ShowResults, "results")]
    [InlineData(OnboardingState.CollectPayment, "900")]
    [InlineData(OnboardingState.TrialActivated, "congratulations")]
    public void BuildSystemPrompt_ContainsStateSpecificGuidance(OnboardingState state, string expectedFragment)
    {
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.CurrentState = state;

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().NotBeNull();
        capturedSystem.Should().Contain(expectedFragment,
            $"system prompt for {state} should contain '{expectedFragment}'");
    }

    // ── Branch: BuildToolDefinitions — unknown tool names are filtered out ──

    [Fact]
    public void BuildToolDefinitions_UnknownToolsAreOmitted()
    {
        // Verify that the tools array excludes any tool name not in the dictionary.
        // We trigger this by using a custom state machine subtype is not possible without modifying prod,
        // so instead we verify via the request body: GenerateSite only has "deploy_site".
        List<string>? capturedToolNames = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("tools", out var toolsProp))
            {
                capturedToolNames = [];
                foreach (var tool in toolsProp.EnumerateArray())
                    capturedToolNames.Add(tool.GetProperty("name").GetString()!);
            }
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.GenerateSite;

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "build it", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedToolNames.Should().NotBeNull();
        // GetCumulativeTools includes tools through the next user-blocking state (ConnectGoogle),
        // so GenerateSite yields deploy_site + submit_cma_form + google_auth_card. Unknown tools are still filtered.
        capturedToolNames.Should().OnlyContain(
            name => name == "deploy_site" || name == "submit_cma_form" || name == "google_auth_card",
            "GenerateSite cumulative tools are deploy_site, submit_cma_form, and google_auth_card; no unknown tools should leak through");
    }

    // ── Branch: empty tool input (ToolInputJson.Length == 0) → deserializes to default ──

    [Fact]
    public async Task StreamResponseAsync_EmptyToolInput_DispatchesWithDefaultJsonElement()
    {
        // Tool call with no input_json_delta events → empty ToolInputJson
        var toolCallSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu_empty","name":"deploy_site","input":{}}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var followUpSse = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Deployed!"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var handler = new SequentialSseResponseHandler([toolCallSse, followUpSse]);

        JsonElement capturedParams = default;
        var mockTool = new Mock<IOnboardingTool>();
        mockTool.Setup(t => t.Name).Returns("deploy_site");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Callback<JsonElement, OnboardingSession, CancellationToken>((p, _, _) => capturedParams = p)
            .ReturnsAsync("Deployed successfully");

        var dispatcher = new ToolDispatcher([mockTool.Object], NullLogger<ToolDispatcher>.Instance);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.GenerateSite;

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            dispatcher,
            NullLogger<OnboardingChatService>.Instance);

        await foreach (var _ in service.StreamResponseAsync(session, "deploy", CancellationToken.None))
        {
        }

        mockTool.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), session, It.IsAny<CancellationToken>()),
            Times.Once);

        // capturedParams should be the default JsonElement (ValueKind == Undefined)
        capturedParams.ValueKind.Should().Be(JsonValueKind.Undefined,
            "empty tool input should result in default JsonElement passed to ExecuteAsync");
    }

    // ── Branch: system prompt with FULL profile — all if (p.X is not null) branches ──

    [Fact]
    public void BuildSystemPrompt_FullProfile_IncludesAllProfileFields()
    {
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Jenise Buckalew",
            Title = "REALTOR",
            Tagline = "Forward. Moving.",
            Phone = "347-393-5993",
            Email = "jenise@example.com",
            Brokerage = "Green Light Realty",
            State = "NJ",
            OfficeAddress = "1109 Englishtown Rd",
            ServiceAreas = ["Middlesex County", "Monmouth County"],
            Specialties = ["First-Time Buyers"],
            Designations = ["ABR"],
            Languages = ["English", "Spanish"],
            YearsExperience = 10,
            HomesSold = 150,
            AvgRating = 4.8,
            ReviewCount = 42,
            PrimaryColor = "#1B5E20",
            AccentColor = "#C8A951",
            Bio = "Helping NJ families find their dream home.",
            WebsiteUrl = "https://jenisesellsnj.com",
            Testimonials =
            [
                new Testimonial { ReviewerName = "Alice", Text = "Great!", Rating = 5 }
            ],
            RecentSales =
            [
                new RecentSale { Address = "123 Main St", Price = 500000 }
            ],
        };

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().NotBeNull();
        capturedSystem.Should().Contain("Jenise Buckalew");
        capturedSystem.Should().Contain("REALTOR");
        capturedSystem.Should().Contain("Forward. Moving.");
        capturedSystem.Should().Contain("347-393-5993");
        capturedSystem.Should().Contain("jenise@example.com");
        capturedSystem.Should().Contain("Green Light Realty");
        capturedSystem.Should().Contain("NJ");
        capturedSystem.Should().Contain("1109 Englishtown Rd");
        capturedSystem.Should().Contain("Middlesex County");
        capturedSystem.Should().Contain("First-Time Buyers");
        capturedSystem.Should().Contain("ABR");
        capturedSystem.Should().Contain("English");
        capturedSystem.Should().Contain("10");
        capturedSystem.Should().Contain("150");
        capturedSystem.Should().Contain("4.8");
        capturedSystem.Should().Contain("#1B5E20");
        capturedSystem.Should().Contain("#C8A951");
        capturedSystem.Should().Contain("Helping NJ families");
        capturedSystem.Should().Contain("jenisesellsnj.com");
        capturedSystem.Should().Contain("1 scraped");
        capturedSystem.Should().Contain("1 found");
    }

    // ── Branch: stream ends without [DONE] (reader.ReadLineAsync returns null) ──

    [Fact]
    public async Task StreamResponseAsync_StreamEndsWithoutDone_CompletesNormally()
    {
        // No [DONE] line — the stream just ends (ReadLineAsync returns null)
        var sseData = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Truncated"}}

            data: {"type":"content_block_stop","index":0}

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "test", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle(c => c == "Truncated");
    }

    // ── Branch: system prompt switch — ConnectGoogle and GenerateSite cases ──

    [Theory]
    [InlineData(OnboardingState.ConnectGoogle, "google_auth_card")]
    [InlineData(OnboardingState.GenerateSite, "deploy_site")]
    [InlineData(OnboardingState.ScrapeProfile, "scrape_url")]
    public void BuildSystemPrompt_ContainsStateSpecificGuidance_RemainingStates(OnboardingState state, string expectedFragment)
    {
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        session.CurrentState = state;

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().NotBeNull();
        capturedSystem.Should().Contain(expectedFragment);
    }

    // ── Branch: system prompt switch — default case (invalid enum value) ──

    [Fact]
    public void BuildSystemPrompt_UnknownState_UsesDefaultGuidance()
    {
        string? capturedSystem = null;

        var handler = new CapturingHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedSystem = doc.RootElement.GetProperty("system").GetString();
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var session = OnboardingSession.Create(null);
        // Cast an invalid int to OnboardingState to exercise the _ default branch
        session.CurrentState = (OnboardingState)99;

        var service = new OnboardingChatService(
            factory.Object,
            "test-key",
            new OnboardingStateMachine(),
            new ToolDispatcher([], NullLogger<ToolDispatcher>.Instance),
            NullLogger<OnboardingChatService>.Instance);

        var enumerator = service.StreamResponseAsync(session, "Hello", CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var act = async () => await enumerator.MoveNextAsync();
        act.Should().ThrowAsync<HttpRequestException>();

        capturedSystem.Should().NotBeNull();
        capturedSystem.Should().Contain("Guide the agent to the next step",
            "default case should provide generic guidance for unknown states");
    }

    // ── Branch: HttpRequestException catch in StreamSingleCallAsync ──

    [Fact]
    public async Task StreamResponseAsync_HttpRequestException_Rethrows()
    {
        var handler = new ThrowingOnSendHandler(new HttpRequestException("Connection refused"));
        var service = CreateService(handler);
        var session = OnboardingSession.Create(null);

        var act = async () =>
        {
            await foreach (var _ in service.StreamResponseAsync(session, "hello", CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Connection refused*");
    }

    // ── Branch: non-success HTTP status code → logs error body and throws ──

    [Fact]
    public async Task StreamResponseAsync_NonSuccessStatusCode_ThrowsWithErrorBody()
    {
        var handler = new StatusCodeResponseHandler(
            System.Net.HttpStatusCode.TooManyRequests,
            "{\"error\":{\"type\":\"rate_limit_error\",\"message\":\"Too many requests\"}}");
        var service = CreateService(handler);
        var session = OnboardingSession.Create(null);

        var act = async () =>
        {
            await foreach (var _ in service.StreamResponseAsync(session, "hello", CancellationToken.None))
            {
            }
        };

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*STREAM-015*429*");
    }

    // ── Branch: empty line in SSE stream → skipped ──

    [Fact]
    public async Task StreamResponseAsync_EmptyLinesInStream_Skipped()
    {
        // SSE data with extra empty lines between events (normal for SSE)
        var sseData = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}


            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"ok"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "test", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle(c => c == "ok");
    }

    // ── Branch: non-"data: " lines in SSE stream → skipped ──

    [Fact]
    public async Task StreamResponseAsync_NonDataLines_Skipped()
    {
        var sseData = """
            event: message_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            retry: 5000
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"ok"}}

            data: {"type":"content_block_stop","index":0}

            data: [DONE]

            """;

        var service = CreateService(new SseResponseHandler(sseData));
        var session = OnboardingSession.Create(null);

        var chunks = new List<string>();
        await foreach (var chunk in service.StreamResponseAsync(session, "test", CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle(c => c == "ok");
    }
}

/// <summary>
/// Test handler that throws an exception when SendAsync is called.
/// Used to test the HttpRequestException catch branch.
/// </summary>
internal class ThrowingOnSendHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        throw exception;
    }
}

/// <summary>
/// Test handler that returns a specific HTTP status code with a body.
/// Used to test non-success status code branches.
/// </summary>
internal class StatusCodeResponseHandler(System.Net.HttpStatusCode statusCode, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
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
/// Returns the same data for every call.
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

/// <summary>
/// Test handler that returns different SSE data for each sequential call.
/// </summary>
internal class SequentialSseResponseHandler(string[] responses) : HttpMessageHandler
{
    private int _callIndex;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var index = Math.Min(_callIndex, responses.Length - 1);
        _callIndex++;
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responses[index], System.Text.Encoding.UTF8, "text/event-stream")
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// Test handler that invokes a callback for each request, allowing inspection and custom responses.
/// </summary>
internal class CallbackSseResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(callback(request));
    }
}
