using System.Diagnostics;
using Google.Apis.Drive.v3;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GDrive;

internal sealed class GDriveApiClient(
    IOAuthRefresher refresher,
    string clientId,
    string clientSecret,
    ILogger<GDriveApiClient> logger) : IGDriveClient
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string TextMimeType = "text/plain";

    public async Task<string> CreateFolderAsync(
        string accountId,
        string agentId,
        string folderPath,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return string.Empty;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.create_folder");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderPath,
                MimeType = FolderMimeType
            };

            var request = service.Files.Create(metadata);
            request.Fields = "id";
            var folder = await request.ExecuteAsync(ct);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-001] Folder created for account {AccountId}, agent {AgentId}. FolderId: {FolderId}, Duration: {Duration}ms",
                accountId, agentId, folder.Id, durationMs);

            return folder.Id ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-031] CreateFolder failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<string> UploadFileAsync(
        string accountId,
        string agentId,
        string folderId,
        string fileName,
        string content,
        CancellationToken ct)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(content);
        return await UploadBinaryAsync(accountId, agentId, folderId, fileName, data, TextMimeType, ct);
    }

    public async Task<string> UploadBinaryAsync(
        string accountId,
        string agentId,
        string folderId,
        string fileName,
        byte[] data,
        string mimeType,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return string.Empty;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.upload");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = [folderId]
            };

            using var stream = new MemoryStream(data);
            var request = service.Files.Create(metadata, stream, mimeType);
            request.Fields = "id";
            await request.UploadAsync(ct);

            var file = request.ResponseBody;
            var fileId = file?.Id ?? string.Empty;

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-003] Binary uploaded for account {AccountId}, agent {AgentId}. FileId: {FileId}, Duration: {Duration}ms",
                accountId, agentId, fileId, durationMs);

            return fileId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-032] UploadBinary failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<string?> DownloadFileAsync(
        string accountId,
        string agentId,
        string folderId,
        string fileName,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return null;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.download");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            // Find file by name within the folder
            var listRequest = service.Files.List();
            listRequest.Q = $"name = '{EscapeQuery(fileName)}' and '{EscapeQuery(folderId)}' in parents and trashed = false";
            listRequest.Fields = "files(id)";
            var listResult = await listRequest.ExecuteAsync(ct);

            var fileId = listResult.Files?.FirstOrDefault()?.Id;
            if (fileId is null)
                return null;

            // Download the file content
            var getRequest = service.Files.Get(fileId);
            using var memStream = new MemoryStream();
            await getRequest.DownloadAsync(memStream, ct);

            var content = System.Text.Encoding.UTF8.GetString(memStream.ToArray());

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-004] File downloaded for account {AccountId}, agent {AgentId}. FileId: {FileId}, Duration: {Duration}ms",
                accountId, agentId, fileId, durationMs);

            return content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-033] DownloadFile failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task DeleteFileAsync(
        string accountId,
        string agentId,
        string fileId,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.delete");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            await service.Files.Delete(fileId).ExecuteAsync(ct);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-005] File deleted for account {AccountId}, agent {AgentId}. FileId: {FileId}, Duration: {Duration}ms",
                accountId, agentId, fileId, durationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-034] DeleteFile failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task DeleteFileByNameAsync(
        string accountId,
        string agentId,
        string folderId,
        string fileName,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.delete_by_name");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var listRequest = service.Files.List();
            listRequest.Q = $"name = '{EscapeQuery(fileName)}' and '{EscapeQuery(folderId)}' in parents and trashed = false";
            listRequest.Fields = "files(id)";
            var listResult = await listRequest.ExecuteAsync(ct);

            var fileId = listResult.Files?.FirstOrDefault()?.Id;
            if (fileId is null)
            {
                logger.LogDebug(
                    "[GDRIVE-007] File not found for delete by name. Account: {AccountId}, Agent: {AgentId}, Folder: {FolderId}, File: {FileName}",
                    accountId, agentId, folderId, fileName);
                return;
            }

            await service.Files.Delete(fileId).ExecuteAsync(ct);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-008] File deleted by name for account {AccountId}, agent {AgentId}. FileId: {FileId}, Duration: {Duration}ms",
                accountId, agentId, fileId, durationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-037] DeleteFileByName failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<List<string>> ListFilesAsync(
        string accountId,
        string agentId,
        string folderId,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return [];

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.list");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var listRequest = service.Files.List();
            listRequest.Q = $"'{EscapeQuery(folderId)}' in parents and trashed = false";
            listRequest.Fields = "files(name)";
            var result = await listRequest.ExecuteAsync(ct);

            var names = result.Files?
                .Select(f => f.Name)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList() ?? [];

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-006] Files listed for account {AccountId}, agent {AgentId}. Count: {Count}, Duration: {Duration}ms",
                accountId, agentId, names.Count, durationMs);

            return names;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-035] ListFiles failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Domain.Shared.Interfaces.External.DriveFileInfo>> ListAllFilesAsync(
        string accountId,
        string agentId,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return [];

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.list_all");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var results = new List<Domain.Shared.Interfaces.External.DriveFileInfo>();
            string? pageToken = null;

            do
            {
                var listRequest = service.Files.List();
                listRequest.Q = "trashed = false";
                listRequest.Fields = "nextPageToken, files(id, name, mimeType, modifiedTime)";
                listRequest.PageSize = 200;
                listRequest.PageToken = pageToken;
                var result = await listRequest.ExecuteAsync(ct);

                if (result.Files is not null)
                {
                    foreach (var f in result.Files)
                    {
                        results.Add(new Domain.Shared.Interfaces.External.DriveFileInfo(
                            f.Id ?? string.Empty,
                            f.Name ?? string.Empty,
                            f.MimeType ?? string.Empty,
                            f.ModifiedTimeDateTimeOffset?.UtcDateTime));
                    }
                }

                pageToken = result.NextPageToken;
            }
            while (pageToken is not null);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-040] ListAllFiles for account {AccountId}, agent {AgentId}. Count: {Count}, Duration: {Duration}ms",
                accountId, agentId, results.Count, durationMs);

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-041] ListAllFiles failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<string?> GetFileContentAsync(
        string accountId,
        string agentId,
        string fileId,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return null;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.get_content");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var getRequest = service.Files.Get(fileId);
            using var memStream = new MemoryStream();
            var downloadProgress = await getRequest.DownloadAsync(memStream, ct);

            if (downloadProgress.Status == Google.Apis.Download.DownloadStatus.Failed)
            {
                logger.LogWarning(
                    "[GDRIVE-043] GetFileContent download failed for account {AccountId}, agent {AgentId}, fileId {FileId}",
                    accountId, agentId, fileId);
                return null;
            }

            var content = System.Text.Encoding.UTF8.GetString(memStream.ToArray());

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-042] GetFileContent for account {AccountId}, agent {AgentId}, fileId {FileId}. Duration: {Duration}ms",
                accountId, agentId, fileId, durationMs);

            return content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-044] GetFileContent failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<string> GetOrCreateFolderAsync(
        string accountId,
        string agentId,
        string folderName,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return string.Empty;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.get_or_create_folder");
        activity?.SetTag("gdrive.account_id", accountId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var listRequest = service.Files.List();
            listRequest.Q = $"name = '{EscapeQuery(folderName)}' and mimeType = '{FolderMimeType}' and 'root' in parents and trashed = false";
            listRequest.Fields = "files(id)";
            var result = await listRequest.ExecuteAsync(ct);

            var existingId = result.Files?.FirstOrDefault()?.Id;
            if (existingId is not null)
            {
                var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
                GDriveDiagnostics.Duration.Record(durationMs);

                logger.LogInformation(
                    "[GDRIVE-045] Folder '{FolderName}' found for account {AccountId}, agent {AgentId}. FolderId: {FolderId}, Duration: {Duration}ms",
                    folderName, accountId, agentId, existingId, durationMs);

                return existingId;
            }

            // Create the folder
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = FolderMimeType
            };

            var createRequest = service.Files.Create(metadata);
            createRequest.Fields = "id";
            var folder = await createRequest.ExecuteAsync(ct);

            var createDurationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(createDurationMs);

            logger.LogInformation(
                "[GDRIVE-046] Folder '{FolderName}' created for account {AccountId}, agent {AgentId}. FolderId: {FolderId}, Duration: {Duration}ms",
                folderName, accountId, agentId, folder.Id, createDurationMs);

            return folder.Id ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-047] GetOrCreateFolder failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<byte[]?> DownloadBinaryAsync(
        string accountId,
        string agentId,
        string fileId,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return null;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.download_binary");
        activity?.SetTag("gdrive.file_id", fileId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var request = service.Files.Get(fileId);
            using var stream = new MemoryStream();
            var downloadProgress = await request.DownloadAsync(stream, ct);

            if (downloadProgress.Status == Google.Apis.Download.DownloadStatus.Failed)
            {
                logger.LogWarning(
                    "[GDRIVE-049] DownloadBinary download failed for account {AccountId}, agent {AgentId}, fileId {FileId}",
                    accountId, agentId, fileId);
                return null;
            }

            var data = stream.ToArray();

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-048] Binary downloaded for account {AccountId}, agent {AgentId}. FileId: {FileId}, Bytes: {Bytes}, Duration: {Duration}ms",
                accountId, agentId, fileId, data.Length, durationMs);

            return data;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-050] DownloadBinary failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    public async Task<string> CopyFileAsync(
        string accountId,
        string agentId,
        string sourceFileId,
        string destinationFolderId,
        string? newName,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return string.Empty;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GDriveDiagnostics.ActivitySource.StartActivity("gdrive.copy_file");
        activity?.SetTag("gdrive.source_file_id", sourceFileId);
        activity?.SetTag("gdrive.destination_folder_id", destinationFolderId);

        try
        {
            GDriveDiagnostics.Operations.Add(1);

            var copyMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Parents = [destinationFolderId],
                Name = newName
            };

            var copyRequest = service.Files.Copy(copyMetadata, sourceFileId);
            copyRequest.Fields = "id";
            var copiedFile = await copyRequest.ExecuteAsync(ct);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GDriveDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GDRIVE-051] File copied for account {AccountId}, agent {AgentId}. SourceFileId: {SourceFileId}, NewFileId: {NewFileId}, Duration: {Duration}ms",
                accountId, agentId, sourceFileId, copiedFile.Id, durationMs);

            return copiedFile.Id ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GDriveDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GDRIVE-052] CopyFile failed for account {AccountId}, agent {AgentId}",
                accountId, agentId);
            throw;
        }
    }

    private async Task<DriveService?> BuildServiceAsync(
        string accountId,
        string agentId,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GDRIVE-010] No valid token for account {AccountId}, agent {AgentId}. Skipping operation.",
                accountId, agentId);
            GDriveDiagnostics.TokenMissing.Add(1);
            return null;
        }

        return BuildDriveService(credential);
    }

    private DriveService BuildDriveService(Domain.Shared.Models.OAuthCredential credential) =>
        new(RealEstateStar.Clients.GoogleOAuth.GoogleCredentialFactory.BuildInitializer(credential, clientId, clientSecret));

    internal static string EscapeQuery(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");
}
