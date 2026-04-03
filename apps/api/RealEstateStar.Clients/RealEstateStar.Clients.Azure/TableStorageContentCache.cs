using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.Clients.Azure;

internal sealed class ContentCacheEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "cache";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Value { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class TableStorageContentCache : IDistributedContentCache
{
    private const string PartitionKey = "cache";

    private readonly TableClient _table;
    private readonly ILogger<TableStorageContentCache> _logger;

    public TableStorageContentCache(TableClient table, ILogger<TableStorageContentCache> logger)
    {
        _table = table;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        try
        {
            var response = await _table.GetEntityIfExistsAsync<ContentCacheEntity>(PartitionKey, key, cancellationToken: ct);

            if (response.Value is null)
            {
                _logger.LogDebug("[CACHE-001] Cache miss for key={Key}", key);
                return null;
            }

            var entity = response.Value;
            if (entity.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                _logger.LogDebug("[CACHE-002] Cache entry expired for key={Key}", key);
                return null;
            }

            var value = JsonSerializer.Deserialize<T>(entity.Value);
            _logger.LogDebug("[CACHE-003] Cache hit for key={Key}", key);
            return value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("[CACHE-001] Cache miss (404) for key={Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        var entity = new ContentCacheEntity
        {
            PartitionKey = PartitionKey,
            RowKey = key,
            Value = JsonSerializer.Serialize(value),
            ExpiresAt = DateTimeOffset.UtcNow + ttl
        };

        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
        _logger.LogDebug("[CACHE-004] Cache set for key={Key} ttl={Ttl}", key, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        try
        {
            await _table.DeleteEntityAsync(PartitionKey, key, ETag.All, ct);
            _logger.LogDebug("[CACHE-005] Cache entry removed for key={Key}", key);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("[CACHE-005] Cache entry already absent for key={Key}", key);
        }
    }
}
