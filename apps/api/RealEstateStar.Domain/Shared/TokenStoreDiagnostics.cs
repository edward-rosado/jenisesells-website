using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Domain.Shared;

public static class TokenStoreDiagnostics
{
    public const string ServiceName = "RealEstateStar.TokenStore";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> Reads = Meter.CreateCounter<long>(
        "tokenstore.reads", description: "Token store read operations");
    public static readonly Counter<long> Writes = Meter.CreateCounter<long>(
        "tokenstore.writes", description: "Token store write operations");
    public static readonly Counter<long> Deletes = Meter.CreateCounter<long>(
        "tokenstore.deletes", description: "Token store delete operations");
    public static readonly Counter<long> Conflicts = Meter.CreateCounter<long>(
        "tokenstore.conflicts", description: "Token store ETag conflicts (optimistic lock failures)");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "tokenstore.duration_ms", unit: "ms", description: "Token store operation duration");
}
