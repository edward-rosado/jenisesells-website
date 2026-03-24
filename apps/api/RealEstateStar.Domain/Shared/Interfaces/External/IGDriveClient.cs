namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IGDriveClient
{
    Task<string> CreateFolderAsync(string accountId, string agentId, string folderPath, CancellationToken ct);
    Task<string> UploadFileAsync(string accountId, string agentId, string folderId,
        string fileName, string content, CancellationToken ct);
    Task<string> UploadBinaryAsync(string accountId, string agentId, string folderId,
        string fileName, byte[] data, string mimeType, CancellationToken ct);
    Task<string?> DownloadFileAsync(string accountId, string agentId, string folderId,
        string fileName, CancellationToken ct);
    Task DeleteFileAsync(string accountId, string agentId, string fileId, CancellationToken ct);
    Task<List<string>> ListFilesAsync(string accountId, string agentId, string folderId, CancellationToken ct);
}
