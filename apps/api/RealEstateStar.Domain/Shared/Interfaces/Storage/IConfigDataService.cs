using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface IConfigDataService
{
    Task<AccountConfig?> GetAccountAsync(string handle, CancellationToken ct);
    Task<List<AccountConfig>> ListAllAsync(CancellationToken ct);
    Task UpdateAccountAsync(string handle, AccountConfig config, CancellationToken ct);
}
