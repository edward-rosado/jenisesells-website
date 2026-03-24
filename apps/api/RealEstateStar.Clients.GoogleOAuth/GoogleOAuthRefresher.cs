using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Clients.GoogleOAuth;

public sealed class GoogleOAuthRefresher : IOAuthRefresher
{
    private readonly ITokenStore _tokenStore;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleOAuthRefresher> _logger;

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public GoogleOAuthRefresher(
        ITokenStore tokenStore,
        string clientId,
        string clientSecret,
        HttpClient httpClient,
        ILogger<GoogleOAuthRefresher> logger)
    {
        _tokenStore = tokenStore;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OAuthCredential?> GetValidCredentialAsync(
        string accountId,
        string agentId,
        CancellationToken ct)
    {
        var credential = await _tokenStore.GetAsync(accountId, agentId, OAuthProviders.Google, ct);
        if (credential is null)
        {
            _logger.LogDebug("[OAUTH-000] No credential found for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            return null;
        }

        if (!credential.IsExpired)
            return credential;

        _logger.LogDebug("[OAUTH-010] Token expired for account {AccountId}, agent {AgentId}. Refreshing.",
            accountId, agentId);

        var refreshed = await RefreshTokenAsync(credential, ct);
        if (refreshed is null)
            return null;

        var saved = await _tokenStore.SaveIfUnchangedAsync(refreshed, credential.ETag!, ct);

        if (!saved)
        {
            _logger.LogDebug("[OAUTH-011] ETag conflict for account {AccountId}, agent {AgentId}. Re-reading.",
                accountId, agentId);
            // Another worker refreshed the token — re-read and return the fresher version
            credential = await _tokenStore.GetAsync(accountId, agentId, OAuthProviders.Google, ct);
            return credential;
        }

        return refreshed;
    }

    internal async Task<OAuthCredential?> RefreshTokenAsync(OAuthCredential credential, CancellationToken ct)
    {
        try
        {
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("refresh_token", credential.RefreshToken),
            });

            var response = await _httpClient.PostAsync(TokenEndpoint, requestBody, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "[OAUTH-020] Token refresh failed for account {AccountId}, agent {AgentId}. Status: {Status}, Body: {Body}",
                    credential.AccountId, credential.AgentId, (int)response.StatusCode, errorBody);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Missing access_token in refresh response");

            var expiresIn = root.TryGetProperty("expires_in", out var expiresInProp)
                ? expiresInProp.GetInt32()
                : 3600;

            var refreshed = credential with
            {
                AccessToken = accessToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                ETag = null  // cleared — SaveIfUnchangedAsync will assign new ETag
            };

            _logger.LogInformation(
                "[OAUTH-001] Token refreshed for account {AccountId}, agent {AgentId}",
                credential.AccountId, credential.AgentId);

            return refreshed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "[OAUTH-030] Unexpected error refreshing token for account {AccountId}, agent {AgentId}",
                credential.AccountId, credential.AgentId);
            return null;
        }
    }
}
