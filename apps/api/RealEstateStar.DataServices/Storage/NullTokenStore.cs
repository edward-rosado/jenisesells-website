namespace RealEstateStar.DataServices.Storage;

/// <summary>
/// Null-object implementation used when Azure Storage is not configured (e.g., local development).
/// Silently discards OAuth token persistence rather than crashing the onboarding flow.
/// </summary>
public sealed class NullTokenStore : ITokenStore
{
    public Task<OAuthCredential?> GetAsync(string accountId, string agentId, string provider, CancellationToken ct) =>
        Task.FromResult<OAuthCredential?>(null);

    public Task SaveAsync(OAuthCredential credential, string provider, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<bool> SaveIfUnchangedAsync(OAuthCredential credential, string provider, string etag, CancellationToken ct) =>
        Task.FromResult(false);

    public Task DeleteAsync(string accountId, string agentId, string provider, CancellationToken ct) =>
        Task.CompletedTask;
}
