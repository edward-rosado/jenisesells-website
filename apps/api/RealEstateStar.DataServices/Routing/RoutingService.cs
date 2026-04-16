using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Routing;

/// <summary>
/// Implements Option C weighted round-robin with override consumption.
/// Algorithm:
///   1. Load policy from IRoutingPolicyStore.
///   2. Compute SHA-256 of policy JSON (policy content hash).
///   3. Load consumption from IBrokerageRoutingConsumptionStore.
///   4. If hash changed or no consumption → reset to fresh consumption (counter=0, override not consumed).
///   5. If NextLead is set and override not yet consumed → route to that agent (CAS mark consumed).
///   6. Else → weighted round-robin across enabled agents, optionally preferring service-area match.
/// </summary>
public sealed class RoutingService(
    IRoutingPolicyStore policyStore,
    IBrokerageRoutingConsumptionStore consumptionStore,
    ILogger<RoutingService> logger)
{
    private const int MaxCasAttempts = 3;
    private const int CasBaseDelayMs = 50;

    public async Task<RoutingDecision> RouteLeadAsync(
        string accountId,
        string leadId,
        string? leadServiceArea,
        CancellationToken ct)
    {
        // 1. Load policy
        var policy = await policyStore.GetPolicyAsync(accountId, ct).ConfigureAwait(false);
        if (policy is null)
        {
            logger.LogError("[ROUTING-050] No routing policy found for accountId={AccountId}", accountId);
            throw new InvalidOperationException(
                $"No routing policy found for account '{accountId}'. Cannot route lead '{leadId}'.");
        }

        // 2. Compute policy content hash
        var policyJson = JsonSerializer.Serialize(policy);
        var policyHash = ComputeSha256(policyJson);

        // 3. Load consumption
        var consumption = await consumptionStore.GetAsync(accountId, ct).ConfigureAwait(false);

        // 4. Reset on hash change or missing consumption
        if (consumption is null || consumption.PolicyContentHash != policyHash)
        {
            logger.LogInformation(
                "[ROUTING-060] Policy hash changed or no consumption record — resetting for accountId={AccountId}",
                accountId);
            consumption = new BrokerageRoutingConsumption
            {
                AccountId = accountId,
                PolicyContentHash = policyHash,
                Counter = 0,
                OverrideConsumed = false,
                LastDecisionAt = DateTime.UtcNow,
                ETag = null
            };
        }

        // 5. Check for NextLead override
        if (!string.IsNullOrEmpty(policy.NextLead) && !consumption.OverrideConsumed)
        {
            var agentId = policy.NextLead;
            var (committed, attemptCount) = await TryCasOverrideAsync(
                accountId, policyHash, consumption, ct).ConfigureAwait(false);

            if (committed)
            {
                logger.LogInformation(
                    "[ROUTING-100] Override consumed: routing lead {LeadId} to agent {AgentId}",
                    leadId, agentId);
                return new RoutingDecision(agentId, "override", attemptCount);
            }

            // CAS conflict — concurrent request consumed the override; fall through to round-robin
            logger.LogWarning(
                "[ROUTING-105] Override CAS conflict for accountId={AccountId} — falling through to round-robin",
                accountId);

            // Re-read consumption after conflict so round-robin has current counter
            consumption = await consumptionStore.GetAsync(accountId, ct).ConfigureAwait(false)
                ?? new BrokerageRoutingConsumption
                {
                    AccountId = accountId,
                    PolicyContentHash = policyHash,
                    Counter = 0,
                    OverrideConsumed = true,
                    LastDecisionAt = DateTime.UtcNow,
                    ETag = null
                };
        }

        // 6. Weighted round-robin
        return await RouteRoundRobinAsync(
            accountId, leadId, leadServiceArea, policy, policyHash, consumption, ct)
            .ConfigureAwait(false);
    }

    // ─── Override CAS ────────────────────────────────────────────────────────

    private async Task<(bool Committed, int AttemptCount)> TryCasOverrideAsync(
        string accountId,
        string policyHash,
        BrokerageRoutingConsumption current,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxCasAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var updated = current with
            {
                OverrideConsumed = true,
                LastDecisionAt = DateTime.UtcNow
            };

            var committed = await consumptionStore.SaveIfUnchangedAsync(updated, ct).ConfigureAwait(false);
            if (committed)
                return (true, attempt);

            if (attempt == MaxCasAttempts)
                break;

            // Re-read for next attempt
            var refreshed = await consumptionStore.GetAsync(accountId, ct).ConfigureAwait(false);
            if (refreshed is null || refreshed.PolicyContentHash != policyHash || refreshed.OverrideConsumed)
            {
                // Override was already consumed by a concurrent caller
                return (false, attempt);
            }

            current = refreshed;

            int backoffMs = CasBaseDelayMs * (1 << (attempt - 1));
            await Task.Delay(backoffMs, ct).ConfigureAwait(false);
        }

        return (false, MaxCasAttempts);
    }

    // ─── Round-robin ─────────────────────────────────────────────────────────

    private async Task<RoutingDecision> RouteRoundRobinAsync(
        string accountId,
        string leadId,
        string? leadServiceArea,
        RoutingPolicy policy,
        string policyHash,
        BrokerageRoutingConsumption consumption,
        CancellationToken ct)
    {
        var enabledAgents = policy.Agents
            .Where(a => a.Enabled)
            .ToList();

        if (enabledAgents.Count == 0)
        {
            logger.LogError("[ROUTING-070] No enabled agents for accountId={AccountId}", accountId);
            throw new InvalidOperationException(
                $"No enabled agents in routing policy for account '{accountId}'.");
        }

        // Service-area preference: prefer agents that serve the lead's area, but don't exclude others
        List<RoutingAgent> candidates;
        if (!string.IsNullOrWhiteSpace(leadServiceArea))
        {
            var preferred = enabledAgents
                .Where(a => a.ServiceAreas.Contains(leadServiceArea, StringComparer.OrdinalIgnoreCase))
                .ToList();

            candidates = preferred.Count > 0 ? preferred : enabledAgents;
        }
        else
        {
            candidates = enabledAgents;
        }

        var counter = consumption.Counter;
        var selected = candidates[counter % candidates.Count];
        var nextCounter = counter + 1;

        var (committed, attemptCount) = await TryCasCounterAsync(
            accountId, policyHash, consumption, nextCounter, ct).ConfigureAwait(false);

        if (!committed)
        {
            // Even if CAS fails after retries, we still route — the counter drift is acceptable
            logger.LogWarning(
                "[ROUTING-115] Counter CAS exhausted for accountId={AccountId} — proceeding with selection anyway",
                accountId);
        }

        logger.LogInformation(
            "[ROUTING-110] Round-robin: routing lead {LeadId} to agent {AgentId} (counter={Counter})",
            leadId, selected.AgentId, counter);

        return new RoutingDecision(selected.AgentId, "round-robin", attemptCount);
    }

    private async Task<(bool Committed, int AttemptCount)> TryCasCounterAsync(
        string accountId,
        string policyHash,
        BrokerageRoutingConsumption current,
        int nextCounter,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxCasAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var updated = current with
            {
                Counter = nextCounter,
                LastDecisionAt = DateTime.UtcNow
            };

            var committed = await consumptionStore.SaveIfUnchangedAsync(updated, ct).ConfigureAwait(false);
            if (committed)
                return (true, attempt);

            if (attempt == MaxCasAttempts)
                break;

            var refreshed = await consumptionStore.GetAsync(accountId, ct).ConfigureAwait(false);
            if (refreshed is null || refreshed.PolicyContentHash != policyHash)
                return (false, attempt);

            // Advance to current counter from refreshed record so our next try uses the latest value
            current = refreshed;
            nextCounter = current.Counter + 1;

            int backoffMs = CasBaseDelayMs * (1 << (attempt - 1));
            await Task.Delay(backoffMs, ct).ConfigureAwait(false);
        }

        return (false, MaxCasAttempts);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
