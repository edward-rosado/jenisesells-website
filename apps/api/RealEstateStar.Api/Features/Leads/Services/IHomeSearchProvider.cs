namespace RealEstateStar.Api.Features.Leads.Services;

public interface IHomeSearchProvider
{
    Task<List<Listing>> SearchAsync(HomeSearchCriteria criteria, CancellationToken ct);
}
