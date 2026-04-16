using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Clients.Azure;

internal sealed class RoutingConsumptionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string PolicyContentHash { get; set; } = string.Empty;
    public int Counter { get; set; }
    public bool OverrideConsumed { get; set; }
    public DateTime LastDecisionAt { get; set; }
}

public sealed class BrokerageRoutingConsumptionStore : IBrokerageRoutingConsumptionStore
{
    private const string ConsumptionRowKey = "consumption";

    private readonly TableClient _table;
    private readonly ILogger<BrokerageRoutingConsumptionStore> _logger;

    public BrokerageRoutingConsumptionStore(
        TableClient table,
        ILogger<BrokerageRoutingConsumptionStore> logger)
    {
        _table = table;
        _logger = logger;
    }

    public async Task<BrokerageRoutingConsumption?> GetAsync(string accountId, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityIfExistsAsync<RoutingConsumptionEntity>(
                accountId, ConsumptionRowKey, cancellationToken: ct);

            if (!response.HasValue || response.Value is null)
            {
                _logger.LogDebug("[ROUTING-001] No consumption record found for accountId={AccountId}", accountId);
                return null;
            }

            var entity = response.Value;
            return new BrokerageRoutingConsumption
            {
                AccountId = accountId,
                PolicyContentHash = entity.PolicyContentHash,
                Counter = entity.Counter,
                OverrideConsumed = entity.OverrideConsumed,
                LastDecisionAt = entity.LastDecisionAt,
                ETag = entity.ETag.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ROUTING-020] Error getting consumption for accountId={AccountId}", accountId);
            throw;
        }
    }

    public async Task<bool> SaveIfUnchangedAsync(BrokerageRoutingConsumption consumption, CancellationToken ct)
    {
        try
        {
            var entity = new RoutingConsumptionEntity
            {
                PartitionKey = consumption.AccountId,
                RowKey = ConsumptionRowKey,
                PolicyContentHash = consumption.PolicyContentHash,
                Counter = consumption.Counter,
                OverrideConsumed = consumption.OverrideConsumed,
                LastDecisionAt = consumption.LastDecisionAt,
                ETag = new ETag(consumption.ETag ?? ETag.All.ToString())
            };

            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
            _logger.LogDebug("[ROUTING-002] Consumption conditionally saved for accountId={AccountId}", consumption.AccountId);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            _logger.LogWarning("[ROUTING-010] ETag conflict saving consumption for accountId={AccountId}", consumption.AccountId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ROUTING-020] Error saving consumption for accountId={AccountId}", consumption.AccountId);
            throw;
        }
    }
}
