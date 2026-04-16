using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface IBrokerageRoutingConsumptionStore
{
    Task<BrokerageRoutingConsumption?> GetAsync(string accountId, CancellationToken ct);
    Task<bool> SaveIfUnchangedAsync(BrokerageRoutingConsumption consumption, CancellationToken ct);
}
