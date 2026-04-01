using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Clients.Anthropic;

public sealed class AnthropicClient(
    IHttpClientFactory httpClientFactory,
    string apiKey,
    ILogger<AnthropicClient> logger) : IAnthropicClient
{
    private const string AnthropicVersion = "2023-06-01";
    private const string ClientName = "Anthropic";

    public async Task<AnthropicResponse> SendAsync(
        string model, string systemPrompt, string userMessage,
        int maxTokens, string pipeline, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();

        using var activity = ClaudeDiagnostics.ActivitySource.StartActivity("claude.send");
        activity?.SetTag("claude.pipeline", pipeline);
        activity?.SetTag("claude.model", model);

        var requestBody = new
        {
            model,
            max_tokens = maxTokens,
            system = new[]
            {
                new
                {
                    type = "text",
                    text = systemPrompt,
                    cache_control = new { type = "ephemeral" }
                }
            },
            messages = new[] { new { role = "user", content = userMessage } }
        };

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);

            // Use per-request HttpRequestMessage to avoid mutating DefaultRequestHeaders,
            // which is not thread-safe with a pooled HttpClient instance.
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
            request.Content = JsonContent.Create(requestBody);
            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CLAUDE-010] Anthropic API error. Pipeline: {Pipeline}, Model: {Model}, Status: {Status}, Body: {Body}",
                    pipeline, model, (int)response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"Anthropic API returned {(int)response.StatusCode}: {errorBody}",
                    null,
                    response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            string content;
            int inputTokens;
            int outputTokens;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                content = root
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;

                var usage = root.GetProperty("usage");
                inputTokens = usage.GetProperty("input_tokens").GetInt32();
                outputTokens = usage.GetProperty("output_tokens").GetInt32();

                // Log cache metrics if present
                if (usage.TryGetProperty("cache_creation_input_tokens", out var cacheCreation) &&
                    usage.TryGetProperty("cache_read_input_tokens", out var cacheRead))
                {
                    var created = cacheCreation.GetInt32();
                    var read = cacheRead.GetInt32();
                    if (created > 0 || read > 0)
                    {
                        logger.LogInformation(
                            "[CLAUDE-025] Prompt cache stats. Pipeline: {Pipeline}, CacheCreated: {CacheCreated}, CacheRead: {CacheRead}",
                            pipeline, created, read);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                logger.LogError(ex, "[CLAUDE-030] Failed to parse Anthropic response. Pipeline: {Pipeline}, Model: {Model}",
                    pipeline, model);
                throw;
            }

            content = StripCodeFences(content);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            ClaudeDiagnostics.RecordUsage(pipeline, model, inputTokens, outputTokens, durationMs);

            logger.LogInformation(
                "[CLAUDE-020] Claude call succeeded. Pipeline: {Pipeline}, Model: {Model}, InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, Duration: {Duration}ms",
                pipeline, model, inputTokens, outputTokens, durationMs);

            return new AnthropicResponse(content, inputTokens, outputTokens, durationMs);
        }
        catch (TaskCanceledException ex)
        {
            ClaudeDiagnostics.RecordFailure(pipeline, model);
            logger.LogWarning(ex, "[CLAUDE-021] Anthropic API call timed out. Pipeline: {Pipeline}, Model: {Model}",
                pipeline, model);
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[CLAUDE-011] API error for {Pipeline}: {Message}", pipeline, ex.Message);
            ClaudeDiagnostics.RecordFailure(pipeline, model);
            throw;
        }
    }

    public async Task<AnthropicResponse> SendWithImagesAsync(
        string model, string systemPrompt, string userMessage,
        IReadOnlyList<(byte[] Data, string MimeType)> images,
        int maxTokens, string pipeline, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();

        using var activity = ClaudeDiagnostics.ActivitySource.StartActivity("claude.send_with_images");
        activity?.SetTag("claude.pipeline", pipeline);
        activity?.SetTag("claude.model", model);
        activity?.SetTag("claude.image_count", images.Count);

        var contentBlocks = new List<object>();
        foreach (var (data, mimeType) in images)
        {
            contentBlocks.Add(new
            {
                type = "image",
                source = new { type = "base64", media_type = mimeType, data = Convert.ToBase64String(data) }
            });
        }
        contentBlocks.Add(new { type = "text", text = userMessage });

        var requestBody = new
        {
            model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = contentBlocks.ToArray() } }
        };

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Content = JsonContent.Create(requestBody);
            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CLAUDE-040] Anthropic Vision API error. Pipeline: {Pipeline}, Model: {Model}, Status: {Status}, Body: {Body}",
                    pipeline, model, (int)response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"Anthropic API returned {(int)response.StatusCode}: {errorBody}",
                    null,
                    response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            string content;
            int inputTokens;
            int outputTokens;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                content = root
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;

                var usage = root.GetProperty("usage");
                inputTokens = usage.GetProperty("input_tokens").GetInt32();
                outputTokens = usage.GetProperty("output_tokens").GetInt32();
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                logger.LogError(ex, "[CLAUDE-050] Failed to parse Anthropic Vision response. Pipeline: {Pipeline}, Model: {Model}",
                    pipeline, model);
                throw;
            }

            content = StripCodeFences(content);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            ClaudeDiagnostics.RecordUsage(pipeline, model, inputTokens, outputTokens, durationMs);

            logger.LogInformation(
                "[CLAUDE-041] Claude Vision call succeeded. Pipeline: {Pipeline}, Model: {Model}, InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, Duration: {Duration}ms",
                pipeline, model, inputTokens, outputTokens, durationMs);

            return new AnthropicResponse(content, inputTokens, outputTokens, durationMs);
        }
        catch (TaskCanceledException ex)
        {
            ClaudeDiagnostics.RecordFailure(pipeline, model);
            logger.LogWarning(ex, "[CLAUDE-042] Anthropic Vision API call timed out. Pipeline: {Pipeline}, Model: {Model}",
                pipeline, model);
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[CLAUDE-043] Vision API error for {Pipeline}: {Message}", pipeline, ex.Message);
            ClaudeDiagnostics.RecordFailure(pipeline, model);
            throw;
        }
    }

    internal static string StripCodeFences(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("```"))
        {
            var inner = trimmed["```json".Length..^"```".Length];
            return inner.Trim();
        }

        if (trimmed.StartsWith("```") && trimmed.EndsWith("```") && trimmed.Length > 6)
        {
            // Strip opening fence line (up to first newline) and closing fence
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                var inner = trimmed[(firstNewline + 1)..^"```".Length];
                return inner.Trim();
            }
        }

        return content;
    }
}
