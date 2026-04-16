using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.Cloudflare;

public static class CloudflareDiagnostics
{
    public const string ServiceName = "RealEstateStar.Cloudflare";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> KvReads = Meter.CreateCounter<long>(
        "cloudflare.kv.reads", description: "Cloudflare KV read operations");
    public static readonly Counter<long> KvWrites = Meter.CreateCounter<long>(
        "cloudflare.kv.writes", description: "Cloudflare KV write operations");
    public static readonly Counter<long> KvDeletes = Meter.CreateCounter<long>(
        "cloudflare.kv.deletes", description: "Cloudflare KV delete operations");
    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>(
        "cloudflare.calls_failed", description: "Failed Cloudflare API calls");
    public static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>(
        "cloudflare.call_duration_ms", description: "Cloudflare API call duration in milliseconds");
}
