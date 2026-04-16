using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Clients.Azure;

internal sealed class PreviewSessionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // sessionId
    public string RowKey { get; set; } = "session";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string SessionId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }
}

/// <summary>
/// Azure Table Storage implementation of <see cref="IPreviewSessionStore"/>.
/// Table: previewsessions
/// PK = sessionId, RK = "session"
/// </summary>
public sealed class PreviewSessionStore : IPreviewSessionStore
{
    private const string RowKey = "session";

    private static readonly TimeSpan SlidingWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan HardCap = TimeSpan.FromDays(30);

    private readonly TableClient _table;
    private readonly ILogger<PreviewSessionStore> _logger;

    public PreviewSessionStore(TableClient table, ILogger<PreviewSessionStore> logger)
    {
        _table = table;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(PreviewSession session, CancellationToken ct)
    {
        var entity = MapToEntity(session);
        try
        {
            // InsertEntity fails with 409 if the entity already exists — exchange token is single-use.
            await _table.AddEntityAsync(entity, ct);
            _logger.LogInformation("[PREVIEW-001] Preview session created. sessionId={SessionId} accountId={AccountId}",
                session.SessionId, session.AccountId);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            _logger.LogWarning("[PREVIEW-002] Exchange token already consumed — sessionId={SessionId} already exists",
                session.SessionId);
            throw new InvalidOperationException(
                $"Exchange token already consumed. SessionId '{session.SessionId}' already exists.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREVIEW-090] Error creating session sessionId={SessionId}", session.SessionId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PreviewSession?> GetAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityIfExistsAsync<PreviewSessionEntity>(sessionId, RowKey, cancellationToken: ct);
            if (!response.HasValue || response.Value is null)
            {
                _logger.LogDebug("[PREVIEW-003] Session not found. sessionId={SessionId}", sessionId);
                return null;
            }

            return MapToDomain(response.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREVIEW-091] Error getting session sessionId={SessionId}", sessionId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityIfExistsAsync<PreviewSessionEntity>(sessionId, RowKey, cancellationToken: ct);
            if (!response.HasValue || response.Value is null)
            {
                _logger.LogWarning("[PREVIEW-004] Revoke requested for non-existent session. sessionId={SessionId}", sessionId);
                throw new KeyNotFoundException($"Session '{sessionId}' not found.");
            }

            var entity = response.Value;

            // Idempotent — already revoked is fine
            if (entity.Revoked)
            {
                _logger.LogInformation("[PREVIEW-005] Session already revoked (idempotent). sessionId={SessionId}", sessionId);
                return;
            }

            entity.Revoked = true;
            entity.RevokedAt = DateTime.UtcNow;

            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
            _logger.LogInformation("[PREVIEW-006] Session revoked. sessionId={SessionId}", sessionId);
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREVIEW-092] Error revoking session sessionId={SessionId}", sessionId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PreviewSession> RefreshExpiryAsync(string sessionId, DateTime createdAt, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityIfExistsAsync<PreviewSessionEntity>(sessionId, RowKey, cancellationToken: ct);
            if (!response.HasValue || response.Value is null)
                throw new KeyNotFoundException($"Session '{sessionId}' not found.");

            var entity = response.Value;

            var hardCapDate = createdAt + HardCap;
            var desired = DateTime.UtcNow + SlidingWindow;
            var newExpiry = desired < hardCapDate ? desired : hardCapDate;

            entity.ExpiresAt = newExpiry;
            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);

            _logger.LogInformation("[PREVIEW-007] Session expiry refreshed. sessionId={SessionId} newExpiry={NewExpiry}",
                sessionId, newExpiry);

            return MapToDomain(entity);
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREVIEW-093] Error refreshing expiry sessionId={SessionId}", sessionId);
            throw;
        }
    }

    private static PreviewSessionEntity MapToEntity(PreviewSession session) => new()
    {
        PartitionKey = session.SessionId,
        RowKey = RowKey,
        SessionId = session.SessionId,
        AccountId = session.AccountId,
        ExpiresAt = session.ExpiresAt,
        Revoked = session.Revoked,
        RevokedAt = session.RevokedAt
    };

    private static PreviewSession MapToDomain(PreviewSessionEntity entity) => new(
        entity.SessionId,
        entity.AccountId,
        entity.ExpiresAt,
        entity.Revoked,
        entity.RevokedAt);
}
