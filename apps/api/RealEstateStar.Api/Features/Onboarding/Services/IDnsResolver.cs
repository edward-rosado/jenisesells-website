using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace RealEstateStar.Api.Features.Onboarding.Services;

/// <summary>
/// Abstracts DNS resolution to allow testing of DNS rebinding prevention.
/// </summary>
public interface IDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string hostname, CancellationToken ct);
}

/// <summary>
/// Production DNS resolver using System.Net.Dns.
/// Excluded from coverage: thin wrapper with no branching logic.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Thin wrapper around Dns.GetHostAddressesAsync — no branching logic to test")]
public class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string hostname, CancellationToken ct)
        => Dns.GetHostAddressesAsync(hostname, ct);
}
