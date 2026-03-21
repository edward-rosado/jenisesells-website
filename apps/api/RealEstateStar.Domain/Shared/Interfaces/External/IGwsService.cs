namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IGwsService
{
    Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath, CancellationToken ct);
    Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath, CancellationToken ct);
    Task<string> CreateDocAsync(string agentEmail, string folderPath, string title, string content, CancellationToken ct);
    Task SendEmailAsync(string agentEmail, string to, string subject, string body, string? attachmentPath, CancellationToken ct);
    Task AppendSheetRowAsync(string agentEmail, string spreadsheetId, List<string> values, CancellationToken ct);
    Task<string> QueryDriveActivityAsync(string agentEmail, string ancestorFolder, DateTime since, CancellationToken ct);

    Task<string?> DownloadDocAsync(string agentEmail, string folder, string fileName, CancellationToken ct);
    Task UpdateDocAsync(string agentEmail, string folder, string fileName, string content, CancellationToken ct);
    Task DeleteDocAsync(string agentEmail, string folder, string fileName, CancellationToken ct);
    Task<List<string>> ListFilesAsync(string agentEmail, string folder, CancellationToken ct);
    Task<List<List<string>>> ReadSheetAsync(string agentEmail, string sheetName, CancellationToken ct);
    Task UpdateSheetRowsAsync(string agentEmail, string sheetName, string filterColumn, string filterValue, string replacementValue, CancellationToken ct);
}
