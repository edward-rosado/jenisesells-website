using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Clients.GoogleOAuth;

/// <summary>
/// Shared factory for building a <see cref="BaseClientService.Initializer"/> from an
/// <see cref="OAuthCredential"/>. Used by all Google API clients (Gmail, GDrive, GDocs, GSheets).
///
/// GoogleOAuth is the shared Google-credential infrastructure layer — the same relationship that
/// Workers.Shared has to Workers.*. Each Google client depends on GoogleOAuth; no other cross-client
/// dependency is permitted.
/// </summary>
public static class GoogleCredentialFactory
{
    /// <summary>
    /// Converts an <see cref="OAuthCredential"/> into a <see cref="BaseClientService.Initializer"/>
    /// that can be passed to any Google API service constructor.
    ///
    /// Builds a <see cref="UserCredential"/> manually (skipping the interactive browser flow that
    /// <c>GoogleWebAuthorizationBroker</c> requires). Token lifecycle is owned by
    /// <see cref="Domain.Shared.Interfaces.Storage.ITokenStore"/>, so a no-op
    /// <see cref="IDataStore"/> is used.
    /// </summary>
    /// <param name="credential">The OAuth credential containing access/refresh tokens.</param>
    /// <param name="clientId">The Google OAuth client ID. Required for token refresh by the Google SDK.</param>
    /// <param name="clientSecret">The Google OAuth client secret. Required for token refresh by the Google SDK.</param>
    public static BaseClientService.Initializer BuildInitializer(
        OAuthCredential credential,
        string clientId,
        string clientSecret)
    {
        var tokenResponse = new TokenResponse
        {
            AccessToken = credential.AccessToken,
            RefreshToken = credential.RefreshToken,
            ExpiresInSeconds = (long)Math.Max(0, (credential.ExpiresAt - DateTime.UtcNow).TotalSeconds),
            IssuedUtc = DateTime.UtcNow
        };

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            },
            Scopes = credential.Scopes,
            DataStore = NullDataStore.Instance
        });

        var userCredential = new UserCredential(flow, credential.Email, tokenResponse);

        return new BaseClientService.Initializer
        {
            HttpClientInitializer = userCredential,
            ApplicationName = "RealEstateStar"
        };
    }
}

/// <summary>
/// No-op <see cref="IDataStore"/> — token lifecycle is managed by
/// <see cref="Domain.Shared.Interfaces.Storage.ITokenStore"/>, not Google's built-in data store.
/// </summary>
internal sealed class NullDataStore : IDataStore
{
    internal static readonly NullDataStore Instance = new();

    public Task StoreAsync<T>(string key, T value) => Task.CompletedTask;
    public Task DeleteAsync<T>(string key) => Task.CompletedTask;
    public Task<T> GetAsync<T>(string key) => Task.FromResult(default(T)!);
    public Task ClearAsync() => Task.CompletedTask;
}
