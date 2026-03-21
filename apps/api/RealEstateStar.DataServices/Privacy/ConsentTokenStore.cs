using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.Privacy;

public class ConsentTokenStore(
    TableClient tableClient,
    ILogger<ConsentTokenStore> logger)
{
    public async Task StoreAsync(string agentId, string tokenHash, Guid leadId, string email, CancellationToken ct)
    {
        var entry = new ConsentTokenEntry
        {
            PartitionKey = agentId,
            RowKey = tokenHash,
            LeadId = leadId,
            EmailHash = ConsentAuditService.ComputeSha256(email),
        };
        await tableClient.UpsertEntityAsync(entry, TableUpdateMode.Replace, ct);
        logger.LogInformation("[CONSENT-005] Consent token stored for lead {LeadId}", leadId);
    }

    public async Task<(Guid LeadId, string EmailHash)?> LookupAsync(string agentId, string tokenHash, CancellationToken ct)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<ConsentTokenEntry>(agentId, tokenHash, cancellationToken: ct);
            return (response.Value.LeadId, response.Value.EmailHash);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task RevokeAsync(string agentId, string tokenHash, CancellationToken ct)
    {
        try
        {
            await tableClient.DeleteEntityAsync(agentId, tokenHash, cancellationToken: ct);
            logger.LogInformation("[CONSENT-006] Consent token revoked for agent {AgentId}", agentId);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Already revoked — idempotent
        }
    }
}
