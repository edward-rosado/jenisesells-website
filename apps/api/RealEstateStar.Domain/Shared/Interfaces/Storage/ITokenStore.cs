using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface ITokenStore
{
    Task<OAuthCredential?> GetAsync(string accountId, string agentId, string provider, CancellationToken ct);
    Task SaveAsync(OAuthCredential credential, CancellationToken ct);
    Task<bool> SaveIfUnchangedAsync(OAuthCredential credential, string etag, CancellationToken ct);
    Task DeleteAsync(string accountId, string agentId, string provider, CancellationToken ct);
}
