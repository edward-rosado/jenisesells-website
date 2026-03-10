using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class GoogleOAuthService(
    HttpClient httpClient,
    string clientId,
    string clientSecret,
    string redirectUri,
    ILogger<GoogleOAuthService> logger)
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";

    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/userinfo.profile",
        "https://www.googleapis.com/auth/userinfo.email",
        "https://www.googleapis.com/auth/gmail.send",
        "https://www.googleapis.com/auth/drive.file",
        "https://www.googleapis.com/auth/documents",
        "https://www.googleapis.com/auth/spreadsheets",
        "https://www.googleapis.com/auth/calendar.events",
    ];

    public virtual (string Url, string Nonce) BuildAuthorizationUrl(string sessionId)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var state = $"{sessionId}:{nonce}";
        var scopeStr = Uri.EscapeDataString(string.Join(" ", Scopes));
        var url = $"{AuthEndpoint}?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={scopeStr}&access_type=offline&prompt=consent&state={Uri.EscapeDataString(state)}";
        return (url, nonce);
    }

    public virtual async Task<GoogleTokens> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        });

        var tokenResponse = await httpClient.PostAsync(TokenEndpoint, tokenRequest, ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(ct);
            logger.LogError("Google token exchange failed: {StatusCode} {Error}", tokenResponse.StatusCode, errorBody);
            throw new InvalidOperationException("Failed to exchange authorization code for tokens");
        }

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(ct);
        var tokenData = JsonDocument.Parse(tokenJson).RootElement;

        var accessToken = tokenData.GetProperty("access_token").GetString()!;
        var refreshToken = tokenData.GetProperty("refresh_token").GetString()!;
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        // Fetch user profile
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await httpClient.SendAsync(profileRequest, ct);
        profileResponse.EnsureSuccessStatusCode();

        var profileJson = await profileResponse.Content.ReadAsStringAsync(ct);
        var profileData = JsonDocument.Parse(profileJson).RootElement;

        var email = profileData.GetProperty("email").GetString()!;
        var name = profileData.GetProperty("name").GetString()!;

        logger.LogInformation("Google OAuth completed for {Email}", email);

        return new GoogleTokens
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            Scopes = Scopes,
            GoogleEmail = email,
            GoogleName = name,
        };
    }

    public virtual async Task RefreshAccessTokenAsync(GoogleTokens tokens, CancellationToken ct)
    {
        var refreshRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = tokens.RefreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token",
        });

        var response = await httpClient.PostAsync(TokenEndpoint, refreshRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Google token refresh failed: {StatusCode} {Error}", response.StatusCode, errorBody);
            throw new InvalidOperationException("Failed to refresh Google access token");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var data = JsonDocument.Parse(json).RootElement;

        tokens.AccessToken = data.GetProperty("access_token").GetString()!;
        tokens.ExpiresAt = DateTime.UtcNow.AddSeconds(data.GetProperty("expires_in").GetInt32());

        logger.LogInformation("Refreshed Google access token for {Email}", tokens.GoogleEmail);
    }
}
