namespace RealEstateStar.Api.Features.Leads.Cma;

public interface ICompAggregator
{
    Task<List<Comp>> FetchCompsAsync(CompSearchRequest request, CancellationToken ct);
}
