namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface IDocumentStorageProvider
{
    Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct);
    Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct);
    Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct);
    Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct);
    Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct);
    Task EnsureFolderExistsAsync(string folder, CancellationToken ct);
}
