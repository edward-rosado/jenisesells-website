using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Anthropic.Tests;

public class AnthropicClientTests
{
    private const string TestModel = "claude-haiku-4-5";
    private const string TestPipeline = "test-pipeline";
    private const string TestApiKey = "test-api-key";

    private static string BuildClaudeResponse(string text, int inputTokens = 100, int outputTokens = 50)
    {
        var response = new
        {
            content = new[] { new { type = "text", text } },
            usage = new { input_tokens = inputTokens, output_tokens = outputTokens }
        };
        return JsonSerializer.Serialize(response);
    }

    private static (AnthropicClient client, MockHttpMessageHandler handler) BuildClient()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Anthropic")).Returns(httpClient);

        var client = new AnthropicClient(factory.Object, TestApiKey, NullLogger<AnthropicClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task SendAsync_ReturnsContent_OnSuccess()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse("Hello from Claude"), Encoding.UTF8, "application/json")
        };

        var result = await client.SendAsync(TestModel, "You are a helpful assistant.", "Say hello.", 100, TestPipeline, CancellationToken.None);

        result.Content.Should().Be("Hello from Claude");
    }

    [Fact]
    public async Task SendAsync_StripsCodeFences()
    {
        var (client, handler) = BuildClient();
        var responseWithFences = "```json\n{\"key\": \"value\"}\n```";
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse(responseWithFences), Encoding.UTF8, "application/json")
        };

        var result = await client.SendAsync(TestModel, "system", "user", 100, TestPipeline, CancellationToken.None);

        result.Content.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public async Task SendAsync_ParsesTokenUsage()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse("response text", inputTokens: 123, outputTokens: 456), Encoding.UTF8, "application/json")
        };

        var result = await client.SendAsync(TestModel, "system", "user", 100, TestPipeline, CancellationToken.None);

        result.InputTokens.Should().Be(123);
        result.OutputTokens.Should().Be(456);
    }

    [Fact]
    public async Task SendAsync_ThrowsOnApiError()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\": \"internal server error\"}", Encoding.UTF8, "application/json")
        };

        var act = async () => await client.SendAsync(TestModel, "system", "user", 100, TestPipeline, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*500*");
    }

    [Theory]
    [InlineData("{\"content\": [], \"usage\": {\"input_tokens\": 10, \"output_tokens\": 5}}")]
    [InlineData("{\"invalid\": true}")]
    public async Task SendAsync_ThrowsOnMalformedResponse(string malformedJson)
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(malformedJson, Encoding.UTF8, "application/json")
        };

        var act = async () => await client.SendAsync(TestModel, "system", "user", 100, TestPipeline, CancellationToken.None);

        // [CLAUDE-030] parse error path — should throw on malformed/missing content[0].text
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendAsync_ThrowsOnTimeout()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Anthropic"))
            .Returns(new HttpClient(new TimeoutThrowingHandler()));

        var client = new AnthropicClient(factory.Object, TestApiKey, NullLogger<AnthropicClient>.Instance);

        var act = async () => await client.SendAsync(TestModel, "system", "user", 100, TestPipeline, CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task SendAsync_ReturnsDurationMs()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse("response"), Encoding.UTF8, "application/json")
        };

        var result = await client.SendAsync(TestModel, "system", "user", 100, TestPipeline, CancellationToken.None);

        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void StripCodeFences_HandlesNoFences()
    {
        var input = "plain response text";

        var result = AnthropicClient.StripCodeFences(input);

        result.Should().Be("plain response text");
    }

    [Fact]
    public void StripCodeFences_HandlesJsonFences()
    {
        var input = "```json\n{\"key\": \"value\"}\n```";

        var result = AnthropicClient.StripCodeFences(input);

        result.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public void StripCodeFences_HandlesGenericFences()
    {
        var input = "```\nsome content\n```";

        var result = AnthropicClient.StripCodeFences(input);

        result.Should().Be("some content");
    }

    [Fact]
    public void StripCodeFences_HandlesJsonFencesWithWhitespace()
    {
        var input = "  ```json\n{\"key\": \"value\"}\n```  ";

        var result = AnthropicClient.StripCodeFences(input);

        result.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public void StripCodeFences_IsCaseInsensitiveForJsonFence()
    {
        var input = "```JSON\n{\"key\": \"value\"}\n```";

        var result = AnthropicClient.StripCodeFences(input);

        result.Should().Be("{\"key\": \"value\"}");
    }

    private sealed class TimeoutThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new TaskCanceledException("Simulated timeout");
    }
}
