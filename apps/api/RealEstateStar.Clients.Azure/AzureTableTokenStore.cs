using System.Diagnostics;
using System.Security.Cryptography;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Clients.Azure;

internal sealed class OAuthTokenEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Scopes { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class AzureTableTokenStore : ITokenStore
{
    private const string DataProtectionPurpose = "RealEstateStar.OAuthTokenStore.Google.v1";

    private readonly TableClient _table;
    private readonly IDataProtector _protector;
    private readonly ILogger<AzureTableTokenStore> _logger;

    public AzureTableTokenStore(TableClient table, IDataProtectionProvider dataProtectionProvider, ILogger<AzureTableTokenStore> logger)
    {
        _table = table;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _logger = logger;
    }

    public async Task<OAuthCredential?> GetAsync(string accountId, string agentId, string provider, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();
        using var activity = TokenStoreDiagnostics.ActivitySource.StartActivity("TokenStore.Get");
        try
        {
            TokenStoreDiagnostics.Reads.Add(1);
            var response = await _table.GetEntityIfExistsAsync<OAuthTokenEntity>(
                accountId, RowKey(agentId, provider), cancellationToken: ct);

            if (!response.HasValue || response.Value is null)
            {
                _logger.LogDebug("[TOKEN-001] Token not found for accountId={AccountId} agentId={AgentId} provider={Provider}",
                    accountId, agentId, provider);
                return null;
            }

            var entity = response.Value;
            var credential = MapToCredential(entity, accountId, agentId);
            if (credential is null)
            {
                _logger.LogError("[TOKEN-021] Decryption failure for token at accountId={AccountId} agentId={AgentId} provider={Provider} — treating as missing to avoid using corrupt token",
                    accountId, agentId, provider);
                return null;
            }
            return credential;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TOKEN-020] Error getting token for accountId={AccountId} agentId={AgentId} provider={Provider}",
                accountId, agentId, provider);
            throw;
        }
        finally
        {
            TokenStoreDiagnostics.Duration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task SaveAsync(OAuthCredential credential, string provider, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();
        using var activity = TokenStoreDiagnostics.ActivitySource.StartActivity("TokenStore.Save");
        try
        {
            TokenStoreDiagnostics.Writes.Add(1);
            var entity = MapToEntity(credential, provider);
            await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
            _logger.LogDebug("[TOKEN-002] Token saved for accountId={AccountId} agentId={AgentId}",
                credential.AccountId, credential.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TOKEN-020] Error saving token for accountId={AccountId} agentId={AgentId}",
                credential.AccountId, credential.AgentId);
            throw;
        }
        finally
        {
            TokenStoreDiagnostics.Duration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task<bool> SaveIfUnchangedAsync(OAuthCredential credential, string provider, string etag, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();
        using var activity = TokenStoreDiagnostics.ActivitySource.StartActivity("TokenStore.SaveIfUnchanged");
        try
        {
            TokenStoreDiagnostics.Writes.Add(1);
            var entity = MapToEntity(credential, provider);
            entity.ETag = new ETag(etag);

            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
            _logger.LogDebug("[TOKEN-002] Token conditionally saved for accountId={AccountId} agentId={AgentId}",
                credential.AccountId, credential.AgentId);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            TokenStoreDiagnostics.Conflicts.Add(1);
            _logger.LogWarning("[TOKEN-010] ETag conflict saving token for accountId={AccountId} agentId={AgentId}",
                credential.AccountId, credential.AgentId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TOKEN-020] Error conditionally saving token for accountId={AccountId} agentId={AgentId}",
                credential.AccountId, credential.AgentId);
            throw;
        }
        finally
        {
            TokenStoreDiagnostics.Duration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task DeleteAsync(string accountId, string agentId, string provider, CancellationToken ct)
    {
        var sw = Stopwatch.GetTimestamp();
        using var activity = TokenStoreDiagnostics.ActivitySource.StartActivity("TokenStore.Delete");
        try
        {
            TokenStoreDiagnostics.Deletes.Add(1);
            await _table.DeleteEntityAsync(accountId, RowKey(agentId, provider), ETag.All, ct);
            _logger.LogDebug("[TOKEN-003] Token deleted for accountId={AccountId} agentId={AgentId} provider={Provider}",
                accountId, agentId, provider);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("[TOKEN-003] Token already absent for accountId={AccountId} agentId={AgentId} provider={Provider}",
                accountId, agentId, provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TOKEN-020] Error deleting token for accountId={AccountId} agentId={AgentId} provider={Provider}",
                accountId, agentId, provider);
            throw;
        }
        finally
        {
            TokenStoreDiagnostics.Duration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private static string RowKey(string agentId, string provider) => $"{agentId}:{provider}";

    private OAuthTokenEntity MapToEntity(OAuthCredential credential, string provider)
    {
        return new OAuthTokenEntity
        {
            PartitionKey = credential.AccountId!,
            RowKey = RowKey(credential.AgentId!, provider),
            AccessToken = Protect(credential.AccessToken),
            RefreshToken = Protect(credential.RefreshToken),
            ExpiresAt = credential.ExpiresAt,
            Scopes = string.Join(" ", credential.Scopes),
            Email = credential.Email,
            Name = credential.Name
        };
    }

    private OAuthCredential? MapToCredential(OAuthTokenEntity entity, string accountId, string agentId)
    {
        var accessToken = Unprotect(entity.AccessToken);
        var refreshToken = Unprotect(entity.RefreshToken);

        // If either token field fails to decrypt, return null so GetAsync treats the credential
        // as missing. This is safer than returning corrupt bytes as an access token — a null
        // credential triggers the "no token" path (re-auth), not a broken API call.
        if (accessToken is null || refreshToken is null)
            return null;

        return new OAuthCredential
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = entity.ExpiresAt,
            Scopes = entity.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            Email = entity.Email,
            Name = entity.Name,
            AccountId = accountId,
            AgentId = agentId,
            ETag = entity.ETag.ToString()
        };
    }

    private string Protect(string plaintext) => _protector.Protect(plaintext);

    private string? Unprotect(string ciphertext)
    {
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException ex)
        {
            // [TOKEN-021] Do NOT fall back to raw ciphertext — passing encrypted bytes as an
            // access token would cause silent API failures. Return null so the caller can
            // treat the credential as missing and require re-authentication.
            _logger.LogError(ex, "[TOKEN-021] Failed to decrypt token value — treating as missing. This may indicate DPAPI key rotation or data corruption.");
            return null;
        }
    }
}
