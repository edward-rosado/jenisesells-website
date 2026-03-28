using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Storage;

/// <summary>
/// Three-tier fan-out storage provider.
/// Document methods (Write/Read/Update/Delete/List/EnsureFolder) fan out to:
///   Agent Drive   — IGDriveClient (accountId, agentId)
///   Account Drive — IGDriveClient (accountId, "__account__")
///   Platform Blob — IDocumentStorageProvider (Azure Blob Storage)
/// All document tiers are best-effort: failure at any tier logs a warning but
/// does not fail the overall operation.
/// Sheet methods (Append/Read/Redact) pass through to IGwsService using
/// the platform spreadsheet convention.
/// </summary>
public sealed class FanOutStorageProvider : IFileStorageProvider
{
    private const string AccountTierAgentId = "__account__";

    private readonly IGDriveClient _driveClient;
    private readonly IGSheetsClient _sheetsClient;
    private readonly IGwsService _gwsService;
    private readonly IDocumentStorageProvider _platformStore;
    private readonly string _accountId;
    private readonly string _agentId;
    private readonly string _platformEmail;
    private readonly ILogger _logger;

    public FanOutStorageProvider(
        IGDriveClient driveClient,
        IGSheetsClient sheetsClient,
        IGwsService gwsService,
        IDocumentStorageProvider platformStore,
        string accountId,
        string agentId,
        string platformEmail,
        ILogger logger)
    {
        _driveClient = driveClient;
        _sheetsClient = sheetsClient;
        _gwsService = gwsService;
        _platformStore = platformStore;
        _accountId = accountId;
        _agentId = agentId;
        _platformEmail = platformEmail;
        _logger = logger;
    }

    // ── Document methods ──────────────────────────────────────────────────────

    public async Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        FanOutDiagnostics.Writes.Add(1);

        var tasks = new[]
        {
            ExecuteTierAsync("Agent", () => _driveClient.UploadFileAsync(_accountId, _agentId, folder, fileName, content, ct)),
            ExecuteTierAsync("Account", () => _driveClient.UploadFileAsync(_accountId, AccountTierAgentId, folder, fileName, content, ct)),
            ExecuteTierAsync("Platform", () => _platformStore.WriteDocumentAsync(folder, fileName, content, ct)),
        };

        await Task.WhenAll(tasks);
        FanOutDiagnostics.Duration.Record(sw.Elapsed.TotalMilliseconds);
    }

    public async Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        // Try Agent first, fall back to Account, then Platform.
        try
        {
            var result = await _driveClient.DownloadFileAsync(_accountId, _agentId, folder, fileName, ct);
            if (result is not null) return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FANOUT-001] Agent tier read failed for {Folder}/{FileName}", folder, fileName);
        }

        try
        {
            var result = await _driveClient.DownloadFileAsync(_accountId, AccountTierAgentId, folder, fileName, ct);
            if (result is not null) return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FANOUT-002] Account tier read failed for {Folder}/{FileName}", folder, fileName);
        }

        try
        {
            return await _platformStore.ReadDocumentAsync(folder, fileName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FANOUT-003] Platform tier read failed for {Folder}/{FileName}", folder, fileName);
            return null;
        }
    }

    public async Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        FanOutDiagnostics.Writes.Add(1);

        var tasks = new[]
        {
            ExecuteTierAsync("Agent", () => _driveClient.UploadFileAsync(_accountId, _agentId, folder, fileName, content, ct)),
            ExecuteTierAsync("Account", () => _driveClient.UploadFileAsync(_accountId, AccountTierAgentId, folder, fileName, content, ct)),
            ExecuteTierAsync("Platform", () => _platformStore.UpdateDocumentAsync(folder, fileName, content, ct)),
        };

        await Task.WhenAll(tasks);
        FanOutDiagnostics.Duration.Record(sw.Elapsed.TotalMilliseconds);
    }

    public async Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        var tasks = new List<Task>
        {
            ExecuteTierAsync("Agent", () => _driveClient.DeleteFileByNameAsync(_accountId, _agentId, folder, fileName, ct)),
            ExecuteTierAsync("Account", () => _driveClient.DeleteFileByNameAsync(_accountId, AccountTierAgentId, folder, fileName, ct)),
            ExecuteTierAsync("Platform", () => _platformStore.DeleteDocumentAsync(folder, fileName, ct)),
        };
        await Task.WhenAll(tasks);
    }

    public async Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
    {
        // Return from Agent tier (primary); fall back to Account then Platform on error OR empty.
        try
        {
            var result = await _driveClient.ListFilesAsync(_accountId, _agentId, folder, ct);
            if (result.Count > 0) return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FANOUT-004] Agent tier list failed for {Folder}; falling back to Account tier", folder);
        }

        try
        {
            var result = await _driveClient.ListFilesAsync(_accountId, AccountTierAgentId, folder, ct);
            if (result.Count > 0) return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FANOUT-005] Account tier list failed for {Folder}; falling back to Platform tier", folder);
        }

        try
        {
            return await _platformStore.ListDocumentsAsync(folder, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FANOUT-006] Platform tier list failed for {Folder}", folder);
            return [];
        }
    }

    // ── Sheet methods — platform-tier passthrough via IGwsService ─────────────

    public async Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct)
        => await _gwsService.AppendSheetRowAsync(_platformEmail, sheetName, values, ct);

    public async Task<List<List<string>>> ReadRowsAsync(
        string sheetName,
        string filterColumn,
        string filterValue,
        CancellationToken ct)
    {
        var all = await _gwsService.ReadSheetAsync(_platformEmail, sheetName, ct);
        if (all.Count == 0) return [];

        // Find column index from header row
        var headers = all[0];
        var colIndex = headers.IndexOf(filterColumn);
        if (colIndex < 0) return []; // Column not found

        // Filter data rows (skip header) by the specific column
        return all.Skip(1)
            .Where(row => colIndex < row.Count && row[colIndex] == filterValue)
            .ToList();
    }

    public async Task RedactRowsAsync(
        string sheetName,
        string filterColumn,
        string filterValue,
        string redactedMarker,
        CancellationToken ct)
        => await _gwsService.UpdateSheetRowsAsync(_platformEmail, sheetName, filterColumn, filterValue, redactedMarker, ct);

    // ── Folder creation — fan-out to all three tiers ──────────────────────────

    public async Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
    {
        var tasks = new[]
        {
            ExecuteTierAsync("Agent", () => _driveClient.CreateFolderAsync(_accountId, _agentId, folder, ct)),
            ExecuteTierAsync("Account", () => _driveClient.CreateFolderAsync(_accountId, AccountTierAgentId, folder, ct)),
            ExecuteTierAsync("Platform", () => _platformStore.EnsureFolderExistsAsync(folder, ct)),
        };

        await Task.WhenAll(tasks);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ExecuteTierAsync(string tierName, Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            FanOutDiagnostics.TierFailures.Add(1, new KeyValuePair<string, object?>("tier", tierName));
            _logger.LogWarning(ex, "[FANOUT-010] {Tier} tier operation failed (best-effort — continuing)", tierName);
        }
    }

    private async Task ExecuteTierAsync(string tierName, Func<Task<string>> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            FanOutDiagnostics.TierFailures.Add(1, new KeyValuePair<string, object?>("tier", tierName));
            _logger.LogWarning(ex, "[FANOUT-010] {Tier} tier operation failed (best-effort — continuing)", tierName);
        }
    }
}
