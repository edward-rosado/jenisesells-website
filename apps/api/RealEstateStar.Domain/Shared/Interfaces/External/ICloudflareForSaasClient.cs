namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface ICloudflareForSaasClient
{
    Task<CustomHostnameResult> CreateCustomHostnameAsync(string hostname, CancellationToken ct);
    Task DeleteCustomHostnameAsync(string hostnameId, CancellationToken ct);
    Task<CustomHostnameResult?> GetCustomHostnameAsync(string hostnameId, CancellationToken ct);
}

public sealed record CustomHostnameResult(string Id, string Hostname, string Status, string? SslStatus);
