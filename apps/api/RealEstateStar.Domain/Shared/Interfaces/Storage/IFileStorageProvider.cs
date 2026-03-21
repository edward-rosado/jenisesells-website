namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface IFileStorageProvider
{
    Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct);
    Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct);
    Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct);
    Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct);
    Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct);

    Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct);
    Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct);
    Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct);

    Task EnsureFolderExistsAsync(string folder, CancellationToken ct);
}
