using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Clients.Anthropic;
using RealEstateStar.Domain.Activation.FieldSpecs;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 5 activity: generates voiced, localized site content for all supported locales.
/// Delegates to <see cref="VoicedContentGenerator"/> for each FieldSpec in the catalog.
///
/// Memory budget: max 2 locales concurrent (SemaphoreSlim(2)) per the 1.5 GB Consumption plan limit.
/// Each locale runs all 15 FieldSpec calls sequentially (no further parallelism within a locale)
/// to keep the peak Claude API response buffer bounded.
///
/// Failure semantics:
/// - If at least one locale fully succeeds → BuildResultType.Full
/// - If ALL locales have every field fall back → BuildResultType.Fallback
/// - Individual field fallbacks within a locale do NOT fail the locale
/// </summary>
public sealed class BuildLocalizedSiteContentFunction(
    VoicedContentGenerator generator,
    ILogger<BuildLocalizedSiteContentFunction> logger)
{
    private const int MaxConcurrentLocales = 2;
    private const string PipelineStep = "BuildLocalizedSiteContent";

    [Function(ActivityNames.BuildLocalizedSiteContent)]
    public async Task<BuildResult> RunAsync(
        [ActivityTrigger] BuildSiteContentInput input,
        CancellationToken ct)
    {
        var memBefore = GC.GetTotalMemory(false) / 1024 / 1024;
        logger.LogInformation(
            "[BUILD-001] BuildLocalizedSiteContent starting. accountId={AccountId} agentId={AgentId} " +
            "locales={Locales} template={Template} memory={MemoryMB}MB",
            input.AccountId, input.AgentId,
            string.Join(",", input.SupportedLocales), input.TemplateName, memBefore);

        try
        {
            if (input.Facts is null)
            {
                logger.LogError(
                    "[BUILD-002] SiteFacts is null for agentId={AgentId}; returning Fallback",
                    input.AgentId);
                return BuildFallbackResult("SiteFacts not provided");
            }

            if (input.SupportedLocales.Count == 0)
            {
                logger.LogWarning(
                    "[BUILD-003] No supported locales for agentId={AgentId}; returning Fallback",
                    input.AgentId);
                return BuildFallbackResult("No supported locales specified");
            }

            var semaphore = new SemaphoreSlim(MaxConcurrentLocales, MaxConcurrentLocales);
            var localeResults = new Dictionary<string, LocaleContentResult>(input.SupportedLocales.Count);
            var localeLock = new object();

            // Fan out: one task per locale, max 2 concurrent
            var localeTasks = input.SupportedLocales.Select(async locale =>
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var result = await GenerateLocaleContentAsync(input, locale, ct).ConfigureAwait(false);
                    lock (localeLock)
                    {
                        localeResults[locale] = result;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(localeTasks).ConfigureAwait(false);

            // Aggregate results
            var successLocales = localeResults
                .Where(kv => kv.Value.HasAnySuccess)
                .ToDictionary(kv => kv.Key, kv => (object)kv.Value.Content);

            var allFailed = successLocales.Count == 0;

            var memAfter = GC.GetTotalMemory(false) / 1024 / 1024;
            logger.LogInformation(
                "[BUILD-004] BuildLocalizedSiteContent complete. agentId={AgentId} " +
                "successLocales={SuccessCount}/{TotalCount} memory={MemoryMB}MB (delta: {DeltaMB}MB)",
                input.AgentId, successLocales.Count, input.SupportedLocales.Count,
                memAfter, memAfter - memBefore);

            if (allFailed)
            {
                var reasons = localeResults.Values
                    .Select(r => r.FallbackReason)
                    .FirstOrDefault(r => r is not null);
                logger.LogWarning(
                    "[BUILD-005] All locales fell back for agentId={AgentId}. Reason: {Reason}",
                    input.AgentId, reasons);
                return BuildFallbackResult(reasons ?? "All locales failed generation");
            }

            return new BuildResult(
                BuildResultType.Full,
                successLocales,
                FallbackReason: null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[BUILD-006] BuildLocalizedSiteContent FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<LocaleContentResult> GenerateLocaleContentAsync(
        BuildSiteContentInput input,
        string locale,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[BUILD-010] Generating content for locale={Locale} agentId={AgentId}",
            locale, input.AgentId);

        // Resolve locale voice — fall back to "en" voice if requested locale has no voice data
        LocaleVoice voice;
        if (input.Facts.VoicesByLocale.TryGetValue(locale, out var exactVoice))
        {
            voice = exactVoice;
        }
        else if (input.Facts.VoicesByLocale.TryGetValue("en", out var enVoice))
        {
            logger.LogWarning(
                "[BUILD-011] No voice data for locale={Locale} agentId={AgentId}; falling back to 'en' voice",
                locale, input.AgentId);
            voice = enVoice;
        }
        else
        {
            logger.LogWarning(
                "[BUILD-012] No voice data available for agentId={AgentId}; using empty voice scaffold",
                input.AgentId);
            voice = new LocaleVoice(locale, "", "", "");
        }

        var content = new Dictionary<string, string>(FieldSpecCatalog.All.Count);
        var fallbackCount = 0;
        string? firstFallbackReason = null;

        // Generate each field sequentially within this locale to bound memory usage
        foreach (var spec in FieldSpecCatalog.All)
        {
            if (ct.IsCancellationRequested)
            {
                logger.LogWarning(
                    "[BUILD-013] Cancellation requested during locale={Locale} generation; stopping",
                    locale);
                break;
            }

            var request = new VoicedRequest<string>(
                Facts: input.Facts,
                Locale: locale,
                Voice: voice,
                Field: spec,
                PipelineStep: PipelineStep);

            VoicedResult<string> result;
            try
            {
                result = await generator.GenerateAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "[BUILD-014] GenerateAsync threw for field={FieldName} locale={Locale} agentId={AgentId}; using fallback",
                    spec.Name, locale, input.AgentId);
                result = new VoicedResult<string>(
                    spec.FallbackValue,
                    IsFallback: true,
                    FailureReason: ex.Message,
                    new ClaudeCallMetrics(0, 0, 0, 0));
            }

            if (result.IsFallback)
            {
                fallbackCount++;
                firstFallbackReason ??= result.FailureReason;
                logger.LogDebug(
                    "[BUILD-015] Field={FieldName} locale={Locale} used fallback: {Reason}",
                    spec.Name, locale, result.FailureReason);
            }
            else
            {
                logger.LogDebug(
                    "[BUILD-016] Field={FieldName} locale={Locale} generated successfully",
                    spec.Name, locale);
            }

            content[spec.Name] = result.Value;
        }

        var totalFields = FieldSpecCatalog.All.Count;
        var allFallback = fallbackCount == totalFields;

        logger.LogInformation(
            "[BUILD-017] Locale={Locale} agentId={AgentId}: {SuccessCount}/{TotalCount} fields generated ({FallbackCount} fallbacks)",
            locale, input.AgentId, totalFields - fallbackCount, totalFields, fallbackCount);

        // Restructure flat key-value pairs into the content.json section hierarchy
        var sectionContent = BuildSectionContent(content);

        return new LocaleContentResult(
            Content: sectionContent,
            HasAnySuccess: !allFallback,
            FallbackReason: allFallback ? firstFallbackReason : null);
    }

    /// <summary>
    /// Converts flat "section.field" keys into a nested Dictionary matching content.json shape:
    /// { "hero": { "headline": "...", "tagline": "...", "cta_text": "..." }, "about": { ... }, ... }
    /// </summary>
    internal static Dictionary<string, object> BuildSectionContent(Dictionary<string, string> flat)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in flat)
        {
            var dotIndex = key.IndexOf('.');
            if (dotIndex < 0)
            {
                // Bare key — put in "meta" bucket
                if (!sections.TryGetValue("meta", out var metaBucket))
                {
                    metaBucket = [];
                    sections["meta"] = metaBucket;
                }
                metaBucket[key] = value;
                continue;
            }

            var section = key[..dotIndex];
            var field = key[(dotIndex + 1)..];

            if (!sections.TryGetValue(section, out var bucket))
            {
                bucket = [];
                sections[section] = bucket;
            }
            bucket[field] = value;
        }

        // Convert inner dictionaries to object for the BuildResult signature
        return sections.ToDictionary(
            kv => kv.Key,
            kv => (object)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static BuildResult BuildFallbackResult(string reason) =>
        new(BuildResultType.Fallback, new Dictionary<string, object>(), reason);

    /// <summary>Internal result bag for a single locale generation pass.</summary>
    private sealed record LocaleContentResult(
        Dictionary<string, object> Content,
        bool HasAnySuccess,
        string? FallbackReason);
}
