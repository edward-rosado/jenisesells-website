using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Services;

public interface IAccountConfigService
{
    Task<AccountConfig?> GetAccountAsync(string handle, CancellationToken ct);
    Task<List<AccountConfig>> ListAllAsync(CancellationToken ct);
}
