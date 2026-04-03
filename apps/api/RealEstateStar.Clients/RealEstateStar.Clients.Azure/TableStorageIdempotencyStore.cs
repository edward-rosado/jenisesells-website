using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.Clients.Azure;

internal sealed class IdempotencyEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}

public sealed class TableStorageIdempotencyStore : IIdempotencyStore
{
    private readonly TableClient _table;
    private readonly ILogger<TableStorageIdempotencyStore> _logger;

    public TableStorageIdempotencyStore(TableClient table, ILogger<TableStorageIdempotencyStore> logger)
    {
        _table = table;
        _logger = logger;
    }

    /// <summary>
    /// Parses the key into PartitionKey + RowKey.
    /// Pattern: {pipeline}:{instanceId}:{step} → PK="{pipeline}:{instanceId}", RK="{step}"
    /// Fallback (fewer than 3 segments): PK=full key, RK="default"
    /// </summary>
    internal static (string partitionKey, string rowKey) ParseKey(string key)
    {
        var segments = key.Split(':', 3);
        if (segments.Length >= 3)
            return ($"{segments[0]}:{segments[1]}", segments[2]);

        return (key, "default");
    }

    public async Task<bool> HasCompletedAsync(string key, CancellationToken ct)
    {
        var (pk, rk) = ParseKey(key);
        try
        {
            var response = await _table.GetEntityIfExistsAsync<IdempotencyEntity>(pk, rk, cancellationToken: ct);
            return response.HasValue && response.Value is not null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IDEMPOTENCY-010] Error checking idempotency for key={Key}", key);
            throw;
        }
    }

    public async Task MarkCompletedAsync(string key, CancellationToken ct)
    {
        var (pk, rk) = ParseKey(key);
        var entity = new IdempotencyEntity
        {
            PartitionKey = pk,
            RowKey = rk,
            CompletedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
            _logger.LogDebug("[IDEMPOTENCY-001] Marked completed for key={Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IDEMPOTENCY-011] Error marking completed for key={Key}", key);
            throw;
        }
    }
}
