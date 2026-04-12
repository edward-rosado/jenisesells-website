using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.EmailTransactionExtraction;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1 activity: extracts structured transaction data from emails.
/// Delegates to <see cref="EmailTransactionExtractor"/>.
///
/// Reusable — designed to be called from both the activation orchestrator
/// and the future email-checking feature.
/// </summary>
public sealed class EmailTransactionExtractionFunction(
    EmailTransactionExtractor extractor,
    ILogger<EmailTransactionExtractionFunction> logger)
{
    [Function(ActivityNames.EmailTransactionExtraction)]
    public async Task<string> RunAsync(
        [ActivityTrigger] EmailTransactionExtractionInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-045] EmailTransactionExtraction for accountId={AccountId}, agentId={AgentId}",
            input.AccountId, input.AgentId);

        try
        {
            var sentEmails = input.EmailCorpus.SentEmails.Select(ActivationDtoMapper.ToDomain).ToList();
            var inboxEmails = input.EmailCorpus.InboxEmails.Select(ActivationDtoMapper.ToDomain).ToList();

            var extractions = await extractor.ExtractAsync(sentEmails, inboxEmails, ct);

            var dtos = extractions.Select(ActivationDtoMapper.ToDto).ToList();
            return JsonSerializer.Serialize(dtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ACTV-FN-046] EmailTransactionExtraction FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
