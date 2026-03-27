using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Cma;

public class CmaPipelineContext : PipelineContext<Lead>
{
    // Step name constants
    public const string StepFetchComps = "fetch-comps";
    public const string StepEnrichSubject = "enrich-subject";
    public const string StepAnalyze = "analyze";
    public const string StepGeneratePdf = "generate-pdf";
    public const string StepNotifySeller = "notify-seller";

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

    public byte[]? PdfBytes
    {
        get => Get<byte[]>("pdf-bytes");
        set { if (value is not null) Set("pdf-bytes", value); }
    }
}
