using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Workers.Shared.Concurrency;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Input DTO for BrokerageJoinFunction.
/// </summary>
public sealed record BrokerageJoinInput
{
    public string AccountId { get; init; } = "";
    public string AgentId { get; init; } = "";
    public string AgentName { get; init; } = "";
    public string CorrelationId { get; init; } = "";
}

/// <summary>
/// Durable Functions activity that atomically adds an agent to a brokerage account.
/// Uses ETag-based CAS via EtagCasRetryPolicy to handle concurrent joins safely.
///
/// Idempotent: if the agent is already in the account's AgentMembers list, the
/// activity returns immediately without performing a write (no CAS needed).
/// </summary>
public sealed class BrokerageJoinFunction(
    IAccountConfigService accountConfigService,
    ILogger<BrokerageJoinFunction> logger)
{
    [Function(ActivityNames.BrokerageJoin)]
    public async Task RunAsync(
        [ActivityTrigger] BrokerageJoinInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACCOUNT-MERGE-001] BrokerageJoin start — accountId={AccountId} agentId={AgentId} correlationId={CorrelationId}",
            input.AccountId, input.AgentId, input.CorrelationId);

        try
        {
            // Phase 1: Read the current account to check idempotency before entering CAS loop.
            var account = await accountConfigService.GetAccountAsync(input.AccountId, ct);

            if (account is null)
            {
                throw new InvalidOperationException(
                    $"[ACCOUNT-MERGE-002] Account '{input.AccountId}' not found — cannot join brokerage.");
            }

            // Phase 2: Idempotency guard. If the agent is already a member, no-op.
            if (IsAlreadyMember(account, input.AgentId))
            {
                logger.LogInformation(
                    "[ACCOUNT-MERGE-010] Agent {AgentId} already in brokerage {AccountId}, no-op",
                    input.AgentId, input.AccountId);
                return;
            }

            // Phase 3: CAS loop — re-read on each attempt to get a fresh ETag.
            var outcome = await EtagCasRetryPolicy.ExecuteAsync(
                maxAttempts: 5,
                attemptFn: async attemptCt =>
                {
                    var fresh = await accountConfigService.GetAccountAsync(input.AccountId, attemptCt);

                    if (fresh is null)
                    {
                        return new Domain.Shared.Models.CasAttemptResult(
                            Committed: false,
                            ShouldRetry: false,
                            Reason: $"Account '{input.AccountId}' disappeared during CAS retry");
                    }

                    // Re-check idempotency on fresh read (another instance may have joined concurrently).
                    if (IsAlreadyMember(fresh, input.AgentId))
                    {
                        logger.LogInformation(
                            "[ACCOUNT-MERGE-010] Agent {AgentId} already in brokerage {AccountId}, no-op (detected in CAS loop)",
                            input.AgentId, input.AccountId);
                        return new Domain.Shared.Models.CasAttemptResult(
                            Committed: true,
                            ShouldRetry: false,
                            Reason: "idempotent — already member");
                    }

                    var updatedMembers = (fresh.AgentMembers ?? [])
                        .Append(new AgentMember
                        {
                            AgentId = input.AgentId,
                            AgentName = input.AgentName,
                            JoinedAt = DateTimeOffset.UtcNow,
                        })
                        .ToList();

                    // Build updated AccountConfig with the new member appended.
                    // AccountConfig uses init-only properties, so we reconstruct with a new instance.
                    var updated = BuildUpdated(fresh, updatedMembers);

                    var etag = fresh.ETag ?? "";
                    var committed = await accountConfigService.SaveIfUnchangedAsync(updated, etag, attemptCt);

                    return new Domain.Shared.Models.CasAttemptResult(
                        Committed: committed,
                        ShouldRetry: !committed,
                        Reason: committed ? null : "ETag mismatch — concurrent write detected");
                },
                logger: logger,
                component: "brokerage-join",
                ct: ct);

            if (!outcome.Succeeded)
            {
                // CAS exhausted — log and throw so Durable Functions retries the entire activity.
                logger.LogError(
                    "[ACCOUNT-MERGE-020] CAS exhausted after {Attempts} attempts for accountId={AccountId} agentId={AgentId}: {Reason}",
                    outcome.AttemptCount, input.AccountId, input.AgentId, outcome.FailureReason);

                throw new InvalidOperationException(
                    $"[ACCOUNT-MERGE-020] BrokerageJoin CAS exhausted for agent '{input.AgentId}' " +
                    $"in account '{input.AccountId}' after {outcome.AttemptCount} attempts: {outcome.FailureReason}");
            }

            logger.LogInformation(
                "[ACCOUNT-MERGE-030] BrokerageJoin succeeded — accountId={AccountId} agentId={AgentId} attempts={Attempts}",
                input.AccountId, input.AgentId, outcome.AttemptCount);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[ACCOUNT-MERGE-040] BrokerageJoin FAILED — accountId={AccountId} agentId={AgentId}: {Message}",
                input.AccountId, input.AgentId, ex.Message);
            throw;
        }
    }

    private static bool IsAlreadyMember(AccountConfig account, string agentId) =>
        account.AgentMembers?.Any(m => m.AgentId == agentId) == true;

    /// <summary>
    /// Returns a new AccountConfig with the updated AgentMembers list.
    /// All other properties are preserved. ETag is carried forward from the fresh read
    /// so SaveIfUnchangedAsync can validate it server-side.
    /// </summary>
    private static AccountConfig BuildUpdated(AccountConfig source, List<AgentMember> updatedMembers)
    {
        // AccountConfig uses init-only properties; copy via object initializer.
        // ETag is intentionally NOT set here — SaveIfUnchangedAsync receives it separately.
        return new AccountConfig
        {
            Handle = source.Handle,
            RawAccountId = source.RawAccountId,
            Template = source.Template,
            Agent = source.Agent,
            Brokerage = source.Brokerage,
            Location = source.Location,
            Branding = source.Branding,
            Integrations = source.Integrations,
            Compliance = source.Compliance,
            ContactInfo = source.ContactInfo,
            AgentMembers = updatedMembers,
            // ETag stays on source; we pass source.ETag ?? "" into SaveIfUnchangedAsync separately.
        };
    }
}
