namespace RealEstateStar.Api.Features.Leads.Submit;

public interface IHomeSearchProvider
{
    Task<List<Listing>> SearchAsync(HomeSearchCriteria criteria, CancellationToken ct);
}
