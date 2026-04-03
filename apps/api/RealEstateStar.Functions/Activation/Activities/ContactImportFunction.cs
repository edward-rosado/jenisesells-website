using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Activation.ContactImportPersist;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 3 activity: persists imported contacts to Drive folders and ILeadStore.
/// Delegates to <see cref="ContactImportPersistActivity"/>.
/// </summary>
public sealed class ContactImportFunction(
    ContactImportPersistActivity activity,
    ILogger<ContactImportFunction> logger)
{
    [Function(ActivityNames.ContactImport)]
    public async Task RunAsync(
        [ActivityTrigger] ContactImportInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-320] ContactImport for accountId={AccountId}, agentId={AgentId}, count={Count}",
            input.AccountId, input.AgentId, input.Contacts.Count);

        var contacts = input.Contacts
            .Select(ActivationDtoMapper.ToDomain)
            .ToList();

        await activity.ExecuteAsync(
            accountId: input.AccountId,
            agentId: input.AgentId,
            contacts: contacts,
            ct: ct);
    }
}
