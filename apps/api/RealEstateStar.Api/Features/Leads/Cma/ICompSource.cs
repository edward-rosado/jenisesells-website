namespace RealEstateStar.Api.Features.Leads.Cma;

public interface ICompSource
{
    string Name { get; }
    Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct);
}
