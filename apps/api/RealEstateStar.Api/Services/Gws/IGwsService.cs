namespace RealEstateStar.Api.Services.Gws;

public interface IGwsService
{
    Task<string> CreateDriveFolderAsync(string agentEmail, string folderPath);
    Task<string> UploadFileAsync(string agentEmail, string folderPath, string filePath);
    Task<string> CreateDocAsync(string agentEmail, string folderPath, string title, string content);
    Task SendEmailAsync(string agentEmail, string to, string subject, string body, string? attachmentPath = null);
    Task AppendSheetRowAsync(string agentEmail, string spreadsheetId, List<string> values);
}
