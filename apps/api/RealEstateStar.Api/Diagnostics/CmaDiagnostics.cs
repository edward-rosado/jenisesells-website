using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Api.Diagnostics;

public static class CmaDiagnostics
{
    public const string ServiceName = "RealEstateStar.Api";
    public const string SourceName = "RealEstateStar.CmaPipeline";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    private static readonly Meter Meter = new(SourceName, "1.0.0");

    public static readonly Counter<long> CmaCreated = Meter.CreateCounter<long>(
        "cma.created", description: "Number of CMA jobs created");

    public static readonly Counter<long> CmaCompleted = Meter.CreateCounter<long>(
        "cma.completed", description: "Number of CMA jobs completed successfully");

    public static readonly Counter<long> CmaFailed = Meter.CreateCounter<long>(
        "cma.failed", description: "Number of CMA jobs that failed");

    public static readonly Histogram<double> CmaDuration = Meter.CreateHistogram<double>(
        "cma.duration", unit: "ms", description: "Total CMA pipeline duration");

    public static readonly Histogram<double> CmaStepDuration = Meter.CreateHistogram<double>(
        "cma.step.duration", unit: "ms", description: "Per-step CMA pipeline duration");
}
