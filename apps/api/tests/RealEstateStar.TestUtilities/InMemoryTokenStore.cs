using System.Collections.Concurrent;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.TestUtilities;

public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, (OAuthCredential Credential, string ETag)> _tokens = new();

    private static string Key(string accountId, string agentId, string provider) =>
        $"{accountId}:{agentId}:{provider}";

    public Task<OAuthCredential?> GetAsync(string accountId, string agentId, string provider, CancellationToken ct)
    {
        if (_tokens.TryGetValue(Key(accountId, agentId, provider), out var entry))
            return Task.FromResult<OAuthCredential?>(entry.Credential with { ETag = entry.ETag });
        return Task.FromResult<OAuthCredential?>(null);
    }

    public Task SaveAsync(OAuthCredential credential, CancellationToken ct)
    {
        var key = Key(credential.AccountId!, credential.AgentId!, OAuthProviders.Google);
        var newETag = Guid.NewGuid().ToString();
        _tokens[key] = (credential with { ETag = newETag }, newETag);
        return Task.CompletedTask;
    }

    public Task<bool> SaveIfUnchangedAsync(OAuthCredential credential, string etag, CancellationToken ct)
    {
        var key = Key(credential.AccountId!, credential.AgentId!, OAuthProviders.Google);
        var newETag = Guid.NewGuid().ToString();
        var newEntry = (credential with { ETag = newETag }, newETag);

        // Atomic compare-and-swap: only update if current entry has matching etag
        while (true)
        {
            if (!_tokens.TryGetValue(key, out var current))
                return Task.FromResult(false); // Key doesn't exist

            if (current.ETag != etag)
                return Task.FromResult(false); // ETag mismatch

            // Attempt atomic update; if it fails (another thread modified), retry
            if (_tokens.TryUpdate(key, newEntry, current))
                return Task.FromResult(true); // Successfully swapped
        }
    }

    public Task DeleteAsync(string accountId, string agentId, string provider, CancellationToken ct)
    {
        _tokens.TryRemove(Key(accountId, agentId, provider), out _);
        return Task.CompletedTask;
    }
}
