using RealEstateStar.Domain.Cma.Models;
namespace RealEstateStar.Domain.Cma.Interfaces;

public interface ICompAggregator
{
    Task<List<Comp>> FetchCompsAsync(CompSearchRequest request, CancellationToken ct);
}
