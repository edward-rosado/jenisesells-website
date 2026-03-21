using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.HomeSearch.Interfaces;

public interface IHomeSearchProvider
{
    Task<List<Listing>> SearchAsync(HomeSearchCriteria criteria, CancellationToken ct);
}
