using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 5 activity: persists generated site content to Cloudflare KV as draft.
///
/// Key schema (version-prefixed for future migration):
///   content:v1:{accountId}:{locale}:draft  — localized site content JSON (IReadOnlyDictionary)
///   account:v1:{accountId}                 — account.json verbatim
///   site-state:v1:{accountId}              — "pending_approval"
///
/// Failure semantics: FATAL — let exceptions propagate so DF retries this activity.
/// KV writes are idempotent; retries are safe.
///
/// Draft-only: promotion to ":live" happens in the publish endpoint (C2/C4).
/// </summary>
public sealed class PersistSiteContentFunction(
    ICloudflareKvClient kvClient,
    IOptions<SiteContentOptions> options,
    ILogger<PersistSiteContentFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false
    };

    [Function(ActivityNames.PersistSiteContent)]
    public async Task RunAsync(
        [ActivityTrigger] PersistSiteContentInput input,
        CancellationToken ct)
    {
        var namespaceId = options.Value.KvNamespaceId;

        logger.LogInformation(
            "[PERSIST-000] PersistSiteContent starting. accountId={AccountId} agentId={AgentId} " +
            "locales={LocaleCount} namespaceId={NamespaceId}",
            input.AccountId, input.AgentId,
            input.BuildResult.ContentByLocale.Count, namespaceId);

        try
        {
            // Step 1: Write per-locale draft content
            foreach (var (locale, content) in input.BuildResult.ContentByLocale)
            {
                var draftKey = $"content:v1:{input.AccountId}:{locale}:draft";
                string serialized;
                try
                {
                    serialized = JsonSerializer.Serialize(content, JsonOpts);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "[PERSIST-020] KV write failed for {AccountId}:{Locale}: serialization error: {Error}",
                        input.AccountId, locale, ex.Message);
                    throw;
                }

                try
                {
                    await kvClient.PutAsync(namespaceId, draftKey, serialized, ct).ConfigureAwait(false);
                    logger.LogInformation(
                        "[PERSIST-001] Wrote draft content for {AccountId}:{Locale}",
                        input.AccountId, locale);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "[PERSIST-020] KV write failed for {AccountId}:{Locale}: {Error}",
                        input.AccountId, locale, ex.Message);
                    throw;
                }
            }

            // Step 2: Write account.json to KV
            var accountKey = $"account:v1:{input.AccountId}";
            try
            {
                await kvClient.PutAsync(namespaceId, accountKey, input.AccountConfigJson, ct).ConfigureAwait(false);
                logger.LogInformation(
                    "[PERSIST-002] Wrote account config for {AccountId}",
                    input.AccountId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[PERSIST-020] KV write failed for {AccountId} account config: {Error}",
                    input.AccountId, ex.Message);
                throw;
            }

            // Step 3: Update site state to pending_approval
            var siteStateKey = $"site-state:v1:{input.AccountId}";
            try
            {
                await kvClient.PutAsync(namespaceId, siteStateKey, "\"pending_approval\"", ct).ConfigureAwait(false);
                logger.LogInformation(
                    "[PERSIST-003] Updated site-state to pending_approval for {AccountId}",
                    input.AccountId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[PERSIST-020] KV write failed for {AccountId} site-state: {Error}",
                    input.AccountId, ex.Message);
                throw;
            }

            logger.LogInformation(
                "[PERSIST-010] Site content persisted for {AccountId}, {LocaleCount} locales",
                input.AccountId, input.BuildResult.ContentByLocale.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[PERSIST-030] PersistSiteContent FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
