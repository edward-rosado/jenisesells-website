using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Anthropic.Tests;

public class AnthropicClientVisionTests
{
    private const string TestModel = "claude-sonnet-4-6";
    private const string TestPipeline = "contact-import";
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
    public async Task SendWithImagesAsync_ReturnsContent_OnSuccess()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse("extracted data"), Encoding.UTF8, "application/json")
        };

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1, 2, 3 }, "image/png")
        };

        var result = await client.SendWithImagesAsync(
            TestModel, "system prompt", "extract data",
            images, 4096, TestPipeline, CancellationToken.None);

        result.Content.Should().Be("extracted data");
    }

    [Fact]
    public async Task SendWithImagesAsync_BuildsCorrectRequestWithImageBlocks()
    {
        var capturedBody = string.Empty;
        var capturingHandler = new CapturingMockHttpMessageHandler(
            async (req, ct) => capturedBody = await req.Content!.ReadAsStringAsync(ct),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildClaudeResponse("extracted data"), Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(capturingHandler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Anthropic")).Returns(httpClient);
        var client = new AnthropicClient(factory.Object, TestApiKey, NullLogger<AnthropicClient>.Instance);

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1, 2, 3 }, "image/png"),
            (new byte[] { 4, 5, 6 }, "image/jpeg")
        };

        await client.SendWithImagesAsync(
            TestModel, "system prompt", "extract data",
            images, 4096, TestPipeline, CancellationToken.None);

        capturedBody.Should().Contain("image/png");
        capturedBody.Should().Contain("image/jpeg");
        capturedBody.Should().Contain("base64");
        capturedBody.Should().Contain(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        capturedBody.Should().Contain(Convert.ToBase64String(new byte[] { 4, 5, 6 }));
    }

    [Fact]
    public async Task SendWithImagesAsync_IncludesTextBlockAfterImages()
    {
        var capturedBody = string.Empty;
        var capturingHandler = new CapturingMockHttpMessageHandler(
            async (req, ct) => capturedBody = await req.Content!.ReadAsStringAsync(ct),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildClaudeResponse("result"), Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(capturingHandler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Anthropic")).Returns(httpClient);
        var client = new AnthropicClient(factory.Object, TestApiKey, NullLogger<AnthropicClient>.Instance);

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1 }, "image/png")
        };

        await client.SendWithImagesAsync(
            TestModel, "system prompt", "my user message",
            images, 4096, TestPipeline, CancellationToken.None);

        using var doc = JsonDocument.Parse(capturedBody);
        var content = doc.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content");

        // First block is image, last block is text
        content[0].GetProperty("type").GetString().Should().Be("image");
        content[1].GetProperty("type").GetString().Should().Be("text");
        content[1].GetProperty("text").GetString().Should().Be("my user message");
    }

    [Fact]
    public async Task SendWithImagesAsync_ParsesTokenUsage()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse("response", inputTokens: 200, outputTokens: 75), Encoding.UTF8, "application/json")
        };

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1, 2 }, "image/png")
        };

        var result = await client.SendWithImagesAsync(
            TestModel, "system", "user", images, 4096, TestPipeline, CancellationToken.None);

        result.InputTokens.Should().Be(200);
        result.OutputTokens.Should().Be(75);
    }

    [Fact]
    public async Task SendWithImagesAsync_ReturnsDurationMs()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse("response"), Encoding.UTF8, "application/json")
        };

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1 }, "image/png")
        };

        var result = await client.SendWithImagesAsync(
            TestModel, "system", "user", images, 4096, TestPipeline, CancellationToken.None);

        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SendWithImagesAsync_StripsCodeFences()
    {
        var (client, handler) = BuildClient();
        var responseWithFences = "```json\n{\"key\": \"value\"}\n```";
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse(responseWithFences), Encoding.UTF8, "application/json")
        };

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1 }, "image/png")
        };

        var result = await client.SendWithImagesAsync(
            TestModel, "system", "user", images, 4096, TestPipeline, CancellationToken.None);

        result.Content.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public async Task SendWithImagesAsync_ThrowsOnApiError()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\": \"internal server error\"}", Encoding.UTF8, "application/json")
        };

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1 }, "image/png")
        };

        var act = async () => await client.SendWithImagesAsync(
            TestModel, "system", "user", images, 4096, TestPipeline, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*500*");
    }

    [Theory]
    [InlineData("{\"content\": [], \"usage\": {\"input_tokens\": 10, \"output_tokens\": 5}}")]
    [InlineData("{\"invalid\": true}")]
    public async Task SendWithImagesAsync_ThrowsOnMalformedResponse(string malformedJson)
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(malformedJson, Encoding.UTF8, "application/json")
        };

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1 }, "image/png")
        };

        var act = async () => await client.SendWithImagesAsync(
            TestModel, "system", "user", images, 4096, TestPipeline, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendWithImagesAsync_ThrowsOnTimeout()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Anthropic"))
            .Returns(new HttpClient(new TimeoutThrowingHandler()));

        var client = new AnthropicClient(factory.Object, TestApiKey, NullLogger<AnthropicClient>.Instance);

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1 }, "image/png")
        };

        var act = async () => await client.SendWithImagesAsync(
            TestModel, "system", "user", images, 4096, TestPipeline, CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task SendWithImagesAsync_SetsCorrectApiKeyHeader()
    {
        var (client, handler) = BuildClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildClaudeResponse("result"), Encoding.UTF8, "application/json")
        };

        var images = new List<(byte[] Data, string MimeType)>
        {
            (new byte[] { 1 }, "image/png")
        };

        await client.SendWithImagesAsync(
            TestModel, "system", "user", images, 4096, TestPipeline, CancellationToken.None);

        handler.LastRequest!.Headers.GetValues("x-api-key").Should().Contain(TestApiKey);
        handler.LastRequest.Headers.GetValues("anthropic-version").Should().Contain("2023-06-01");
    }

    private sealed class TimeoutThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new TaskCanceledException("Simulated timeout");
    }

    /// <summary>
    /// Captures request content before the HttpRequestMessage is disposed,
    /// necessary because AnthropicClient uses `using var request`.
    /// </summary>
    private sealed class CapturingMockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task> captureAction,
        HttpResponseMessage responseToReturn) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await captureAction(request, ct);
            return responseToReturn;
        }
    }
}
