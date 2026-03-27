using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.HomeSearch;

public class HomeSearchPipelineContext : PipelineContext<Lead>
{
    // Step name constants
    public const string StepFetchListings = "fetch-listings";

    // Request-level data passed from the dispatch payload
    public required TaskCompletionSource<HomeSearchWorkerResult> Completion { get; init; }
    public required AgentNotificationConfig AgentConfig { get; init; }

    // Typed accessors
    public List<Listing>? Listings
    {
        get => Get<List<Listing>>("listings");
        set { if (value is not null) Set("listings", value); }
    }
}
