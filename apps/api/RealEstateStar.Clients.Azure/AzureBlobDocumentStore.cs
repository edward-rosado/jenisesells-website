using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Clients.Azure;

/// <summary>
/// Azure Blob Storage-backed document store for the Platform tier.
/// Blob path convention: {folder}/{fileName} maps to blob names under the container.
/// The container is created on first write if it does not exist.
/// </summary>
public sealed class AzureBlobDocumentStore : IDocumentStorageProvider
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobDocumentStore> _logger;
    private bool _containerEnsured;

    public AzureBlobDocumentStore(
        BlobContainerClient container,
        ILogger<AzureBlobDocumentStore> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var blobPath = BlobPath(folder, fileName);
        var blob = _container.GetBlobClient(blobPath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true, ct);
        _logger.LogInformation("[BLOB-001] Written {BlobPath}", blobPath);
    }

    public async Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var blobPath = BlobPath(folder, fileName);
        var blob = _container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(ct)) return null;
        var response = await blob.DownloadContentAsync(ct);
        return response.Value.Content.ToString();
    }

    public async Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
        => await WriteDocumentAsync(folder, fileName, content, ct);

    public async Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var blobPath = BlobPath(folder, fileName);
        var blob = _container.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
        _logger.LogInformation("[BLOB-002] Deleted (or absent) {BlobPath}", blobPath);
    }

    public async Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);
        var prefix = folder.EndsWith('/') ? folder : $"{folder}/";
        var results = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            results.Add(item.Name.Substring(prefix.Length));
        }
        return results;
    }

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
    {
        // Blob storage has no real folders — they are virtual path prefixes.
        // Container creation is handled lazily on first write; nothing else is needed here.
        return Task.CompletedTask;
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerEnsured) return;
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        _containerEnsured = true;
    }

    private static string BlobPath(string folder, string fileName) => $"{folder}/{fileName}";
}
