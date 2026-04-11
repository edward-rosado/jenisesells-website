using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;
using RealEstateStar.Workers.Activation.EmailClassification;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 1.5 activity: classifies all emails into categories using a lightweight Claude call.
/// Delegates to <see cref="EmailClassificationWorker"/>.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class EmailClassificationFunction(
    EmailClassificationWorker worker,
    ILogger<EmailClassificationFunction> logger)
{
    [Function(ActivityNames.EmailClassification)]
    public async Task<string> RunAsync(
        [ActivityTrigger] EmailClassificationInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[ACTV-FN-017] EmailClassification for agentId={AgentId}", input.AgentId);

        try
        {
            var emailCorpus = ActivationDtoMapper.ToDomain(input.EmailCorpus);
            var result = await worker.ClassifyAsync(emailCorpus, ct);

            return JsonSerializer.Serialize(new EmailClassificationOutput
            {
                Classifications = result.Classifications.Select(c => new ClassifiedEmailDto
                {
                    EmailId = c.EmailId,
                    Categories = c.Categories.Select(cat => cat.ToString()).ToList()
                }).ToList(),
                Summary = new CorpusSummaryDto
                {
                    TotalEmails = result.Summary.TotalEmails,
                    TransactionCount = result.Summary.TransactionCount,
                    MarketingCount = result.Summary.MarketingCount,
                    FeeRelatedCount = result.Summary.FeeRelatedCount,
                    ComplianceCount = result.Summary.ComplianceCount,
                    LeadNurtureCount = result.Summary.LeadNurtureCount,
                    LanguageDistribution = result.Summary.LanguageDistribution,
                    DominantTone = result.Summary.DominantTone,
                    AverageEmailLength = result.Summary.AverageEmailLength
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[ACTV-FN-018] EmailClassification FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }
}
