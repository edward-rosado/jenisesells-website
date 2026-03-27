using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Cma;

public class CmaPipelineContext : PipelineContext<Lead>
{
    // Step name constants
    public const string StepFetchComps = "fetch-comps";
    public const string StepAnalyze = "analyze";

    /// <summary>The original processing request, held so ProcessAsync can access Completion.</summary>
    public required CmaProcessingRequest ProcessingRequest { get; init; }

    // Typed accessors
    public List<Comp>? Comps
    {
        get => Get<List<Comp>>("comps");
        set { if (value is not null) Set("comps", value); }
    }

    public CmaAnalysis? Analysis
    {
        get => Get<CmaAnalysis>("analysis");
        set { if (value is not null) Set("analysis", value); }
    }
}
