using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Features.Cma.Services.Comps;

public interface ICompSource
{
    string Name { get; }

    Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct);
}
