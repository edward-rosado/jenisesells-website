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
    Task DeleteFileByNameAsync(string accountId, string agentId, string folderId, string fileName, CancellationToken ct);
    Task<List<string>> ListFilesAsync(string accountId, string agentId, string folderId, CancellationToken ct);

    /// <summary>List all files in the agent's Drive (across all folders).</summary>
    Task<IReadOnlyList<DriveFileInfo>> ListAllFilesAsync(string accountId, string agentId, CancellationToken ct);

    /// <summary>Download raw text content of a file by its Drive file ID.</summary>
    Task<string?> GetFileContentAsync(string accountId, string agentId, string fileId, CancellationToken ct);

    /// <summary>Find or create a top-level folder by name. Returns the folder ID.</summary>
    Task<string> GetOrCreateFolderAsync(string accountId, string agentId, string folderName, CancellationToken ct);

    /// <summary>Download raw binary content of a file by its Drive file ID. Returns null if the token is missing or the download fails.</summary>
    Task<byte[]?> DownloadBinaryAsync(string accountId, string agentId, string fileId, CancellationToken ct);

    /// <summary>Copy a file to a destination folder and optionally rename it. Returns the new file ID.</summary>
    Task<string> CopyFileAsync(string accountId, string agentId, string sourceFileId,
        string destinationFolderId, string? newName, CancellationToken ct);
}

/// <summary>Lightweight metadata returned from listing Drive files.</summary>
public sealed record DriveFileInfo(
    string Id,
    string Name,
    string MimeType,
    DateTime? ModifiedTime);
