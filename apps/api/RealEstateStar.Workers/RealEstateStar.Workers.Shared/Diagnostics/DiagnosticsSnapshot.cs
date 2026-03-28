namespace RealEstateStar.Workers.Shared.Diagnostics;

public record DiagnosticsSnapshot(
    string ServiceName,
    Dictionary<string, long> Counters,
    Dictionary<string, double> Histograms,
    DateTime CollectedAt);
