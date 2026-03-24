using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.GoogleOAuth.Tests;

public class GoogleOAuthRefresherTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";

    private static OAuthCredential ValidCredential(bool expired = false) => new()
    {
        AccountId = AccountId,
        AgentId = AgentId,
        AccessToken = "access-token",
        RefreshToken = "refresh-token",
        ExpiresAt = expired
            ? DateTime.UtcNow.AddMinutes(-10)
            : DateTime.UtcNow.AddHours(1),
        Scopes = ["https://mail.google.com/"],
        Email = "agent@example.com",
        Name = "Test Agent",
        ETag = "etag-123"
    };

    private static string BuildTokenResponse(string accessToken = "new-access-token", int expiresIn = 3600)
    {
        return JsonSerializer.Serialize(new
        {
            access_token = accessToken,
            expires_in = expiresIn,
            token_type = "Bearer"
        });
    }

    private static (GoogleOAuthRefresher Refresher, InMemoryTokenStore Store, MockHttpMessageHandler Handler)
        BuildRefresher(HttpResponseMessage? httpResponse = null)
    {
        var store = new InMemoryTokenStore();
        var handler = new MockHttpMessageHandler();
        if (httpResponse is not null)
            handler.ResponseToReturn = httpResponse;
        var httpClient = new HttpClient(handler);
        var refresher = new GoogleOAuthRefresher(
            store, ClientId, ClientSecret, httpClient, NullLogger<GoogleOAuthRefresher>.Instance);
        return (refresher, store, handler);
    }

    [Fact]
    public async Task GetValidCredentialAsync_ReturnsCredential_WhenNotExpired()
    {
        var (refresher, store, _) = BuildRefresher();
        var credential = ValidCredential(expired: false);
        await store.SaveAsync(credential, CancellationToken.None);

        var result = await refresher.GetValidCredentialAsync(AccountId, AgentId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task GetValidCredentialAsync_ReturnsNull_WhenNotFound()
    {
        var (refresher, _, _) = BuildRefresher();

        var result = await refresher.GetValidCredentialAsync(AccountId, AgentId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidCredentialAsync_RefreshesToken_WhenExpired()
    {
        var tokenResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse("refreshed-token"), Encoding.UTF8, "application/json")
        };
        var (refresher, store, _) = BuildRefresher(tokenResponse);
        var credential = ValidCredential(expired: true);
        await store.SaveAsync(credential, CancellationToken.None);

        var result = await refresher.GetValidCredentialAsync(AccountId, AgentId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("refreshed-token");
    }

    [Fact]
    public async Task GetValidCredentialAsync_ReturnsNull_WhenRefreshFails()
    {
        var tokenResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", Encoding.UTF8, "application/json")
        };
        var (refresher, store, _) = BuildRefresher(tokenResponse);
        var credential = ValidCredential(expired: true);
        await store.SaveAsync(credential, CancellationToken.None);

        var result = await refresher.GetValidCredentialAsync(AccountId, AgentId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidCredentialAsync_ReReads_OnETagConflict()
    {
        // Simulate ETag conflict by having another credential save after we read but before SaveIfUnchangedAsync
        // This is tested by using a token store that returns a stale ETag on save.
        var tokenResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse("refreshed-by-us"), Encoding.UTF8, "application/json")
        };
        var (refresher, store, _) = BuildRefresher(tokenResponse);

        // Set up: store a credential with ETag "etag-stale"
        var credential = ValidCredential(expired: true);
        await store.SaveAsync(credential, CancellationToken.None);

        // Simulate conflict: override ETag stored internally by saving again (different ETag)
        // Then directly test the ETag conflict path via RefreshTokenAsync + SaveIfUnchangedAsync
        // We test the full flow: if GetValidCredentialAsync re-reads on conflict it returns non-null
        // Because InMemoryTokenStore will refuse SaveIfUnchangedAsync if ETag doesn't match,
        // we need to mutate the stored ETag first.
        // Simplest approach: save a NEW credential to update the ETag, but keep the internal state...
        // The ETagConflict scenario requires a concurrency store — we verify the behavior via
        // the StaleETagTokenStore below.
        var staleETagStore = new StaleETagTokenStore(credential);
        var staleETagRefresher = new GoogleOAuthRefresher(
            staleETagStore, ClientId, ClientSecret, new HttpClient(new MockHttpMessageHandler
            {
                ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildTokenResponse("refreshed-by-other-worker"), Encoding.UTF8, "application/json")
                }
            }), NullLogger<GoogleOAuthRefresher>.Instance);

        var result = await staleETagRefresher.GetValidCredentialAsync(AccountId, AgentId, CancellationToken.None);

        // Should return the credential re-read from store after ETag conflict
        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("winner-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_OnUnexpectedException()
    {
        // Use a throwing handler to simulate unexpected error
        var throwingHandler = new ThrowingHttpHandler();
        var store = new InMemoryTokenStore();
        var refresher = new GoogleOAuthRefresher(
            store, ClientId, ClientSecret, new HttpClient(throwingHandler),
            NullLogger<GoogleOAuthRefresher>.Instance);

        var credential = ValidCredential(expired: true);
        var result = await refresher.RefreshTokenAsync(credential, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_PropagatesCancellation()
    {
        var cancellingHandler = new CancellingHttpHandler();
        var store = new InMemoryTokenStore();
        var refresher = new GoogleOAuthRefresher(
            store, ClientId, ClientSecret, new HttpClient(cancellingHandler),
            NullLogger<GoogleOAuthRefresher>.Instance);

        var credential = ValidCredential(expired: true);
        var act = async () => await refresher.RefreshTokenAsync(credential, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_UpdatesExpiresAt_BasedOnExpiresIn()
    {
        var tokenResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildTokenResponse("new-token", expiresIn: 7200), Encoding.UTF8, "application/json")
        };
        var (refresher, _, _) = BuildRefresher(tokenResponse);
        var credential = ValidCredential(expired: true);

        var result = await refresher.RefreshTokenAsync(credential, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(7200), TimeSpan.FromSeconds(5));
    }

    // Helpers for ETag conflict test
    private sealed class StaleETagTokenStore : Domain.Shared.Interfaces.Storage.ITokenStore
    {
        private readonly OAuthCredential _expiredCredential;
        private int _getCallCount;

        public StaleETagTokenStore(OAuthCredential expiredCredential)
        {
            _expiredCredential = expiredCredential;
        }

        public Task<OAuthCredential?> GetAsync(string accountId, string agentId, string provider, CancellationToken ct)
        {
            _getCallCount++;
            if (_getCallCount == 1)
                return Task.FromResult<OAuthCredential?>(_expiredCredential);

            // Second call (after ETag conflict re-read): return the "winner" token
            return Task.FromResult<OAuthCredential?>(new OAuthCredential
            {
                AccountId = accountId,
                AgentId = agentId,
                AccessToken = "winner-token",
                RefreshToken = "winner-refresh",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Scopes = [],
                Email = "agent@example.com",
                Name = "Test Agent",
                ETag = "winner-etag"
            });
        }

        // Always returns false to simulate ETag conflict
        public Task<bool> SaveIfUnchangedAsync(OAuthCredential credential, string etag, CancellationToken ct)
            => Task.FromResult(false);

        public Task SaveAsync(OAuthCredential credential, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(string accountId, string agentId, string provider, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("Simulated network failure");
    }

    private sealed class CancellingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new OperationCanceledException("Simulated cancellation");
    }
}
