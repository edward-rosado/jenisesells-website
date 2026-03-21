using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Data;

/// <summary>
/// Noop implementation of IFileStorageProvider used until Google Drive integration
/// is wired up. Silently drops all read/write calls so the pipeline can run without Drive.
/// </summary>
public class NoopFileStorageProvider(ILogger<NoopFileStorageProvider> logger) : IFileStorageProvider
{
    public Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-001] Noop: WriteDocument {Folder}/{FileName}", folder, fileName);
        return Task.CompletedTask;
    }

    public Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-002] Noop: ReadDocument {Folder}/{FileName}", folder, fileName);
        return Task.FromResult<string?>(null);
    }

    public Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-003] Noop: UpdateDocument {Folder}/{FileName}", folder, fileName);
        return Task.CompletedTask;
    }

    public Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-004] Noop: DeleteDocument {Folder}/{FileName}", folder, fileName);
        return Task.CompletedTask;
    }

    public Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-005] Noop: ListDocuments {Folder}", folder);
        return Task.FromResult(new List<string>());
    }

    public Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-006] Noop: AppendRow {SheetName}", sheetName);
        return Task.CompletedTask;
    }

    public Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn,
        string filterValue, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-007] Noop: ReadRows {SheetName}", sheetName);
        return Task.FromResult(new List<List<string>>());
    }

    public Task RedactRowsAsync(string sheetName, string filterColumn,
        string filterValue, string redactedMarker, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-008] Noop: RedactRows {SheetName}", sheetName);
        return Task.CompletedTask;
    }

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
    {
        logger.LogDebug("[STORAGE-009] Noop: EnsureFolder {Folder}", folder);
        return Task.CompletedTask;
    }
}
