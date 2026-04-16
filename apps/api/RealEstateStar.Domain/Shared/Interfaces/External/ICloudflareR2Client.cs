namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface ICloudflareR2Client
{
    Task PutObjectAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream?> GetObjectAsync(string bucket, string key, CancellationToken ct);
    Task DeleteObjectAsync(string bucket, string key, CancellationToken ct);
}
