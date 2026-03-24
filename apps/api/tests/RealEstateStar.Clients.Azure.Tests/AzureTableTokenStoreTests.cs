using FluentAssertions;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.Azure.Tests;

public sealed class AzureTableTokenStoreTests
{
    private static OAuthCredential BuildCredential(string accountId = "acct-1", string agentId = "agent-1") =>
        new()
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["https://mail.google.com/"],
            Email = "agent@example.com",
            Name = "Agent One",
            AccountId = accountId,
            AgentId = agentId
        };

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        var store = new InMemoryTokenStore();
        using var cts = new CancellationTokenSource();

        var result = await store.GetAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_StoresCredential()
    {
        var store = new InMemoryTokenStore();
        var credential = BuildCredential();
        using var cts = new CancellationTokenSource();

        await store.SaveAsync(credential, cts.Token);
        var result = await store.GetAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);

        result.Should().NotBeNull();
        result!.Email.Should().Be(credential.Email);
        result.AccessToken.Should().Be(credential.AccessToken);
        result.RefreshToken.Should().Be(credential.RefreshToken);
    }

    [Fact]
    public async Task GetAsync_ReturnsStoredCredential_WithETag()
    {
        var store = new InMemoryTokenStore();
        var credential = BuildCredential();
        using var cts = new CancellationTokenSource();

        await store.SaveAsync(credential, cts.Token);
        var result = await store.GetAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);

        result.Should().NotBeNull();
        result!.ETag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveIfUnchangedAsync_ReturnsFalse_OnETagMismatch()
    {
        var store = new InMemoryTokenStore();
        var credential = BuildCredential();
        using var cts = new CancellationTokenSource();

        await store.SaveAsync(credential, cts.Token);

        var staleETag = "stale-etag-does-not-match";
        var result = await store.SaveIfUnchangedAsync(credential, staleETag, cts.Token);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SaveIfUnchangedAsync_ReturnsTrue_OnETagMatch()
    {
        var store = new InMemoryTokenStore();
        var credential = BuildCredential();
        using var cts = new CancellationTokenSource();

        await store.SaveAsync(credential, cts.Token);
        var stored = await store.GetAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);
        stored.Should().NotBeNull();

        var etag = stored!.ETag!;
        var updated = credential with { Name = "Updated Name" };
        var result = await store.SaveIfUnchangedAsync(updated, etag, cts.Token);

        result.Should().BeTrue();
        var afterUpdate = await store.GetAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);
        afterUpdate!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_RemovesCredential()
    {
        var store = new InMemoryTokenStore();
        var credential = BuildCredential();
        using var cts = new CancellationTokenSource();

        await store.SaveAsync(credential, cts.Token);
        await store.DeleteAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);
        var result = await store.GetAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_IsIdempotent_WhenNotFound()
    {
        var store = new InMemoryTokenStore();
        using var cts = new CancellationTokenSource();

        // Should not throw when deleting non-existent entry
        await store.Invoking(s => s.DeleteAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConcurrentRefresh_OnlyOneWins()
    {
        var store = new InMemoryTokenStore();
        var credential = BuildCredential();
        using var cts = new CancellationTokenSource();

        // Save initial credential
        await store.SaveAsync(credential, cts.Token);
        var stored = await store.GetAsync("acct-1", "agent-1", OAuthProviders.Google, cts.Token);
        var sharedETag = stored!.ETag!;

        // Two threads attempt to update using the same (original) ETag
        var updatedA = credential with { AccessToken = "new-access-token-A" };
        var updatedB = credential with { AccessToken = "new-access-token-B" };

        var task1 = store.SaveIfUnchangedAsync(updatedA, sharedETag, cts.Token);
        var task2 = store.SaveIfUnchangedAsync(updatedB, sharedETag, cts.Token);

        var results = await Task.WhenAll(task1, task2);

        // Exactly one should succeed and one should fail (ETag mismatch)
        var successes = results.Count(r => r);
        var failures = results.Count(r => !r);

        successes.Should().Be(1);
        failures.Should().Be(1);
    }
}
