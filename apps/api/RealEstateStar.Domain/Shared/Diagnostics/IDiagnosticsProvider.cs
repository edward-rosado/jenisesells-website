namespace RealEstateStar.Domain.Shared.Diagnostics;

/// <summary>
/// Implemented by each worker project to expose its metrics for the /diagnostics endpoint.
/// </summary>
public interface IDiagnosticsProvider
{
    string ServiceName { get; }
    DiagnosticsSnapshot GetSnapshot();
}
