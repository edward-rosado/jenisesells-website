using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Lead.ContactDetection;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 2.5 activity: detects and classifies contacts from Drive extractions and email corpus.
/// Delegates to <see cref="ContactDetectionActivity"/>.
/// </summary>
public sealed class ContactDetectionFunction(
    ContactDetectionActivity activity,
    ILogger<ContactDetectionFunction> logger)
{
    [Function(ActivityNames.ContactDetection)]
    public async Task<ContactDetectionOutput> RunAsync(
        [ActivityTrigger] ContactDetectionInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-250] ContactDetection for agentId={AgentId}", input.AgentId);

        var driveExtractions = input.DriveExtractions
            .Select(ActivationDtoMapper.ToDomain)
            .ToList();

        var emailCorpus = ActivationDtoMapper.ToDomain(input.EmailCorpus);

        var contacts = await activity.ExecuteAsync(driveExtractions, emailCorpus, ct);

        return new ContactDetectionOutput(
            Contacts: contacts.Select(ActivationDtoMapper.ToDto).ToList());
    }
}
