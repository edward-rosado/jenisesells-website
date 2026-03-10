namespace RealEstateStar.Api.Services.Gws;

public interface IGwsService
{
    Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath, CancellationToken ct);
    Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath, CancellationToken ct);
    Task<string> CreateDocAsync(string agentEmail, string folderPath, string title, string content, CancellationToken ct);
    Task SendEmailAsync(string agentEmail, string to, string subject, string body, string? attachmentPath, CancellationToken ct);
    Task AppendSheetRowAsync(string agentEmail, string spreadsheetId, List<string> values, CancellationToken ct);
}
