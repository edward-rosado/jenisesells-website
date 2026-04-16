using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface IRoutingPolicyStore
{
    Task<RoutingPolicy?> GetPolicyAsync(string accountId, CancellationToken ct);
}
