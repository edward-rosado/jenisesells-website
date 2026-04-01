using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Activities.Lead.ContactDetection;

internal static class ContactDetectionDiagnostics
{
    internal const string ServiceName = "RealEstateStar.ContactDetection";

    internal static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    /// <summary>
    /// Number of contacts imported, tagged by pipeline stage.
    /// Tag key: "stage" with values "Lead", "ActiveClient", "UnderContract", "Closed".
    /// </summary>
    internal static readonly Counter<long> ContactsImported = Meter.CreateCounter<long>(
        "contacts.imported",
        description: "Number of contacts imported from Drive extractions and inbox emails, by pipeline stage");

    /// <summary>
    /// Number of duplicate contacts merged during classification.
    /// </summary>
    internal static readonly Counter<long> DuplicatesMerged = Meter.CreateCounter<long>(
        "contacts.duplicates_merged",
        description: "Number of duplicate contacts merged during deduplication");
}
