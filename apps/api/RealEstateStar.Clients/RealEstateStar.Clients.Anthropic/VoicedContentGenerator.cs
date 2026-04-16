using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Clients.Anthropic;

/// <summary>
/// Generates voiced content for a single declared field.
/// Cache-first: checks IDistributedContentCache before calling Claude.
/// On any failure (Claude error, validation failure), returns the field's FallbackValue.
/// </summary>
public sealed class VoicedContentGenerator(
    IAnthropicClient claude,
    IDistributedContentCache cache,
    ILogger<VoicedContentGenerator> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public async Task<VoicedResult<T>> GenerateAsync<T>(
        VoicedRequest<T> request, CancellationToken ct)
        where T : class
    {
        var cacheKey = BuildCacheKey(request);

        // 1. Cache check
        try
        {
            var cached = await cache.GetAsync<T>(cacheKey, ct);
            if (cached is not null)
            {
                logger.LogInformation(
                    "[VCG-010] Cache hit for {FieldName} locale={Locale}",
                    request.Field.Name, request.Locale);

                return new VoicedResult<T>(cached, IsFallback: false, FailureReason: null, ZeroMetrics());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VCG-011] Cache read failed for {FieldName}; proceeding to Claude call",
                request.Field.Name);
        }

        // 2. Call Claude
        AnthropicResponse response;
        var sw = Stopwatch.GetTimestamp();

        try
        {
            var systemPrompt = BuildSystemPrompt(request);
            response = await claude.SendAsync(
                request.Field.Model,
                systemPrompt,
                request.Field.PromptTemplate,
                request.Field.MaxOutputTokens,
                request.PipelineStep,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[VCG-040] Falling back for {FieldName}: Claude call threw {ExceptionType}",
                request.Field.Name, ex.GetType().Name);

            return new VoicedResult<T>(
                request.Field.FallbackValue,
                IsFallback: true,
                FailureReason: ex.Message,
                ZeroMetrics());
        }

        // 3. Parse value from Claude response
        T value;
        try
        {
            value = ParseResponse<T>(response.Content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[VCG-040] Falling back for {FieldName}: response parsing threw {ExceptionType}",
                request.Field.Name, ex.GetType().Name);

            return new VoicedResult<T>(
                request.Field.FallbackValue,
                IsFallback: true,
                FailureReason: $"Parse error: {ex.Message}",
                BuildMetrics(request.Field.Model, response, sw));
        }

        // 4. Validation
        if (request.Field.Validator is not null && !request.Field.Validator(value))
        {
            logger.LogWarning(
                "[VCG-030] Schema validation failed for {FieldName}; using fallback",
                request.Field.Name);

            return new VoicedResult<T>(
                request.Field.FallbackValue,
                IsFallback: true,
                FailureReason: "Validation failed",
                BuildMetrics(request.Field.Model, response, sw));
        }

        // 5. Record usage metrics
        var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
        ClaudeDiagnostics.RecordUsage(
            request.PipelineStep,
            request.Field.Model,
            response.InputTokens,
            response.OutputTokens,
            durationMs,
            pipelineStep: request.PipelineStep);

        var metrics = new ClaudeCallMetrics(
            response.InputTokens,
            response.OutputTokens,
            EstimateCost(request.Field.Model, response.InputTokens, response.OutputTokens),
            durationMs);

        // 6. Cache write
        try
        {
            await cache.SetAsync(cacheKey, value!, CacheTtl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[VCG-012] Cache write failed for {FieldName}; result still returned",
                request.Field.Name);
        }

        return new VoicedResult<T>(value, IsFallback: false, FailureReason: null, metrics);
    }

    // --- helpers ---

    internal static string BuildCacheKey<T>(VoicedRequest<T> request) =>
        $"vcg:{request.Field.Name}:{request.Facts.FactsHash}:{request.Locale}:{request.Voice.VoiceHash}";

    private static string BuildSystemPrompt<T>(VoicedRequest<T> request)
    {
        var agent = request.Facts.Agent;
        return $"""
                You are writing content for {agent.Name}'s real estate website.
                Voice guidance: {request.Voice.VoiceSkillMarkdown}
                Personality: {request.Voice.PersonalitySkillMarkdown}
                Locale: {request.Locale}
                """;
    }

    private static T ParseResponse<T>(string content)
    {
        if (typeof(T) == typeof(string))
        {
            // For string fields, the content IS the value
            return (T)(object)content;
        }

        // For other types, attempt JSON deserialization
        return System.Text.Json.JsonSerializer.Deserialize<T>(content)
               ?? throw new InvalidOperationException($"Deserialized value for type {typeof(T).Name} was null");
    }

    private static ClaudeCallMetrics BuildMetrics(string model, AnthropicResponse response, long swStart)
    {
        var durationMs = Stopwatch.GetElapsedTime(swStart).TotalMilliseconds;
        return new ClaudeCallMetrics(
            response.InputTokens,
            response.OutputTokens,
            EstimateCost(model, response.InputTokens, response.OutputTokens),
            durationMs);
    }

    private static double EstimateCost(string model, int inputTokens, int outputTokens)
    {
        var (inputRate, outputRate) = model switch
        {
            var m when m.Contains("haiku") => (0.80 / 1_000_000, 4.0 / 1_000_000),
            var m when m.Contains("opus") => (15.0 / 1_000_000, 75.0 / 1_000_000),
            _ => (3.0 / 1_000_000, 15.0 / 1_000_000) // sonnet default
        };
        return inputTokens * inputRate + outputTokens * outputRate;
    }

    private static ClaudeCallMetrics ZeroMetrics() =>
        new(InputTokens: 0, OutputTokens: 0, EstimatedCostUsd: 0, DurationMs: 0);
}
