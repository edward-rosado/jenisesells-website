using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.HomeSearch;

public class HomeSearchPipelineContext : PipelineContext<Lead>
{
    // Step name constants
    public const string StepFetchListings = "fetch-listings";
    public const string StepNotifyBuyer = "notify-buyer";

    // Typed accessors
    public List<Listing>? Listings
    {
        get => Get<List<Listing>>("listings");
        set { if (value is not null) Set("listings", value); }
    }
}
