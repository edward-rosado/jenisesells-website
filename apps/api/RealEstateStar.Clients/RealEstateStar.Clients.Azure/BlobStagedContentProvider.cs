using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;

namespace RealEstateStar.Clients.Azure;

/// <summary>
/// Azure Blob Storage-backed staged content provider for the activation pipeline.
/// Stores Drive file contents as individual blobs during activation, then cleans up after persist.
///
/// Blob path convention: activation-staging/{accountId}/{agentId}/{driveFileId}.txt
/// Container: lead-documents (shared with other platform data)
/// </summary>
public sealed class BlobStagedContentProvider : IStagedContentProvider
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobStagedContentProvider> _logger;
    private bool _containerEnsured;

    public BlobStagedContentProvider(
        BlobContainerClient container,
        ILogger<BlobStagedContentProvider> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task StageContentAsync(
        string accountId, string agentId, string driveFileId, string content, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var blobPath = BlobPath(accountId, agentId, driveFileId);
        var blob = _container.GetBlobClient(blobPath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true, ct);
    }

    public async Task<string?> GetContentAsync(
        string accountId, string agentId, string driveFileId, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var blobPath = BlobPath(accountId, agentId, driveFileId);
        var blob = _container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(ct)) return null;
        var response = await blob.DownloadContentAsync(ct);
        return response.Value.Content.ToString();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllContentsAsync(
        string accountId, string agentId, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var prefix = StagingPrefix(accountId, agentId);
        var result = new Dictionary<string, string>();

        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            var fileName = item.Name[(prefix.Length)..];
            var driveFileId = Path.GetFileNameWithoutExtension(fileName);
            var blob = _container.GetBlobClient(item.Name);
            var response = await blob.DownloadContentAsync(ct);
            result[driveFileId] = response.Value.Content.ToString();
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetTopContentsAsync(
        string accountId, string agentId, int maxFiles, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var prefix = StagingPrefix(accountId, agentId);
        var result = new Dictionary<string, string>();

        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            if (result.Count >= maxFiles) break;
            var fileName = item.Name[(prefix.Length)..];
            var driveFileId = Path.GetFileNameWithoutExtension(fileName);
            var blob = _container.GetBlobClient(item.Name);
            var response = await blob.DownloadContentAsync(ct);
            result[driveFileId] = response.Value.Content.ToString();
        }

        return result;
    }

    public async Task<int> GetCountAsync(string accountId, string agentId, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var prefix = StagingPrefix(accountId, agentId);
        var count = 0;
        await foreach (var _ in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
            count++;
        return count;
    }

    public async Task CleanupAsync(string accountId, string agentId, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var prefix = StagingPrefix(accountId, agentId);
        var deleted = 0;

        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            await _container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: ct);
            deleted++;
        }

        _logger.LogInformation(
            "[STAGED-010] Cleaned up {Count} staged content blobs for accountId={AccountId}, agentId={AgentId}",
            deleted, accountId, agentId);
    }

    private static string StagingPrefix(string accountId, string agentId) =>
        $"activation-staging/{accountId}/{agentId}/";

    private static string BlobPath(string accountId, string agentId, string driveFileId) =>
        $"activation-staging/{accountId}/{agentId}/{driveFileId}.txt";

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerEnsured) return;
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        _containerEnsured = true;
    }
}
