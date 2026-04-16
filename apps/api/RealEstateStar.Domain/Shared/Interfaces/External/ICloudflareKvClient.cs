namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface ICloudflareKvClient
{
    Task<string?> GetAsync(string namespaceId, string key, CancellationToken ct);
    Task PutAsync(string namespaceId, string key, string value, CancellationToken ct);
    Task DeleteAsync(string namespaceId, string key, CancellationToken ct);
    Task<IReadOnlyList<string>> ListKeysAsync(string namespaceId, string? prefix, CancellationToken ct);
}
