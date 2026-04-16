using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface IAccountConfigService
{
    Task<AccountConfig?> GetAccountAsync(string handle, CancellationToken ct);
    Task<List<AccountConfig>> ListAllAsync(CancellationToken ct);
    Task UpdateAccountAsync(string handle, AccountConfig config, CancellationToken ct);

    /// <summary>
    /// Atomic compare-and-swap update. Returns true if committed, false on ETag mismatch (412).
    /// Caller should re-read and retry via EtagCasRetryPolicy.ExecuteAsync.
    /// </summary>
    Task<bool> SaveIfUnchangedAsync(AccountConfig account, string etag, CancellationToken ct);
}
