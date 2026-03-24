using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Clients.Anthropic;

internal sealed class AnthropicClient(
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
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            client.DefaultRequestHeaders.Remove("x-api-key");
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Remove("anthropic-version");
            client.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);

            var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", requestBody, ct);

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
                "[CLAUDE-001] Claude call succeeded. Pipeline: {Pipeline}, Model: {Model}, InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, Duration: {Duration}ms",
                pipeline, model, inputTokens, outputTokens, durationMs);

            return new AnthropicResponse(content, inputTokens, outputTokens, durationMs);
        }
        catch (TaskCanceledException ex)
        {
            ClaudeDiagnostics.RecordFailure(pipeline, model);
            logger.LogWarning(ex, "[CLAUDE-020] Anthropic API call timed out. Pipeline: {Pipeline}, Model: {Model}",
                pipeline, model);
            throw;
        }
        catch (HttpRequestException)
        {
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
