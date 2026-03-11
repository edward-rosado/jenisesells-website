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
/// </summary>
public class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string hostname, CancellationToken ct)
        => Dns.GetHostAddressesAsync(hostname, ct);
}
