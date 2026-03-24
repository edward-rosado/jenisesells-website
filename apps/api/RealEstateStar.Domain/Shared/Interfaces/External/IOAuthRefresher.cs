using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IOAuthRefresher
{
    Task<OAuthCredential?> GetValidCredentialAsync(string accountId, string agentId, CancellationToken ct);
}
