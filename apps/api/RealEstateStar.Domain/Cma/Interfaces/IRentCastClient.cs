using RealEstateStar.Domain.Cma.Models;

namespace RealEstateStar.Domain.Cma.Interfaces;

public interface IRentCastClient
{
    Task<RentCastValuation?> GetValuationAsync(string address, CancellationToken ct);
}
