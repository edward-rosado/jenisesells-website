using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Api.Services.Storage;

public class GDriveStorageProvider(IGwsService gwsService, string agentEmail) : IFileStorageProvider
{
    public Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
        => gwsService.CreateDocAsync(agentEmail, folder, fileName, content, ct);

    public Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
        => gwsService.DownloadDocAsync(agentEmail, folder, fileName, ct);

    public Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
        => gwsService.UpdateDocAsync(agentEmail, folder, fileName, content, ct);

    public Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
        => gwsService.DeleteDocAsync(agentEmail, folder, fileName, ct);

    public Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
        => gwsService.ListFilesAsync(agentEmail, folder, ct);

    public Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct)
        => gwsService.AppendSheetRowAsync(agentEmail, sheetName, values, ct);

    public async Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct)
    {
        var allRows = await gwsService.ReadSheetAsync(agentEmail, sheetName, ct);
        return allRows.Where(row => row.Contains(filterValue)).ToList();
    }

    public Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct)
        => gwsService.UpdateSheetRowsAsync(agentEmail, sheetName, filterColumn, filterValue, redactedMarker, ct);

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
        => gwsService.CreateDriveFolderAsync(agentEmail, folder, ct);
}
