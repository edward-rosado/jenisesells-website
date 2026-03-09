namespace RealEstateStar.Api.Services.Gws;

public interface IGwsService
{
    Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath, CancellationToken ct = default);
    Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath, CancellationToken ct = default);
    Task<string> CreateDocAsync(string agentEmail, string folderPath, string title, string content, CancellationToken ct = default);
    Task SendEmailAsync(string agentEmail, string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default);
    Task AppendSheetRowAsync(string agentEmail, string spreadsheetId, List<string> values, CancellationToken ct = default);
}
