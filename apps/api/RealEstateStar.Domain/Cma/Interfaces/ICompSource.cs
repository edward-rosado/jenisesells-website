using RealEstateStar.Domain.Cma.Models;
namespace RealEstateStar.Domain.Cma.Interfaces;

public interface ICompSource
{
    string Name { get; }
    Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct);
}
