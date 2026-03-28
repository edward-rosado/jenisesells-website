using System.Diagnostics;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GSheets;

internal sealed class GSheetsApiClient(
    IOAuthRefresher refresher,
    string clientId,
    string clientSecret,
    ILogger<GSheetsApiClient> logger) : IGSheetsClient
{
    public async Task AppendRowAsync(
        string accountId,
        string agentId,
        string spreadsheetId,
        string sheetName,
        List<string> values,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GSheetsDiagnostics.ActivitySource.StartActivity("gsheets.append");
        activity?.SetTag("gsheets.account_id", accountId);

        try
        {
            var range = $"{sheetName}!A1";
            var valueRange = new ValueRange
            {
                Values = [values.Cast<object>().ToList()]
            };

            var request = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync(ct);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GSheetsDiagnostics.Operations.Add(1);
            GSheetsDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GSHEETS-001] Row appended for account {AccountId}, agent {AgentId}, sheet {SheetName}. Duration: {Duration}ms",
                accountId, agentId, sheetName, durationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GSheetsDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GSHEETS-031] Sheets append failed for account {AccountId}, agent {AgentId}, sheet {SheetName}",
                accountId, agentId, sheetName);
            throw;
        }
    }

    public async Task<List<List<string>>> ReadRowsAsync(
        string accountId,
        string agentId,
        string spreadsheetId,
        string sheetName,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return [];

        var sw = Stopwatch.GetTimestamp();
        using var activity = GSheetsDiagnostics.ActivitySource.StartActivity("gsheets.read");
        activity?.SetTag("gsheets.account_id", accountId);

        try
        {
            var range = $"{sheetName}";
            var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
            var response = await request.ExecuteAsync(ct);

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GSheetsDiagnostics.Operations.Add(1);
            GSheetsDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GSHEETS-002] Rows read for account {AccountId}, agent {AgentId}, sheet {SheetName}. Duration: {Duration}ms",
                accountId, agentId, sheetName, durationMs);

            if (response.Values is null)
                return [];

            return response.Values
                .Select(row => row.Select(cell => cell?.ToString() ?? string.Empty).ToList())
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GSheetsDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GSHEETS-032] Sheets read failed for account {AccountId}, agent {AgentId}, sheet {SheetName}",
                accountId, agentId, sheetName);
            throw;
        }
    }

    public async Task RedactRowsAsync(
        string accountId,
        string agentId,
        string spreadsheetId,
        string sheetName,
        string filterColumn,
        string filterValue,
        string redactedMarker,
        CancellationToken ct)
    {
        var service = await BuildServiceAsync(accountId, agentId, ct);
        if (service is null)
            return;

        var sw = Stopwatch.GetTimestamp();
        using var activity = GSheetsDiagnostics.ActivitySource.StartActivity("gsheets.redact");
        activity?.SetTag("gsheets.account_id", accountId);

        try
        {

            // Read the sheet to find headers and matching rows
            var range = $"{sheetName}";
            var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, range);
            var response = await getRequest.ExecuteAsync(ct);

            if (response.Values is null || response.Values.Count == 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
                GSheetsDiagnostics.Operations.Add(1);
                GSheetsDiagnostics.Duration.Record(elapsed);
                logger.LogInformation(
                    "[GSHEETS-003] No rows to redact for account {AccountId}, agent {AgentId}, sheet {SheetName}. Duration: {Duration}ms",
                    accountId, agentId, sheetName, elapsed);
                return;
            }

            var headers = response.Values[0].Select(h => h?.ToString() ?? string.Empty).ToList();
            var filterColIndex = headers.IndexOf(filterColumn);

            if (filterColIndex < 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
                GSheetsDiagnostics.Operations.Add(1);
                GSheetsDiagnostics.Duration.Record(elapsed);
                logger.LogInformation(
                    "[GSHEETS-003] Filter column '{FilterColumn}' not found in sheet {SheetName} for account {AccountId}, agent {AgentId}. Duration: {Duration}ms",
                    filterColumn, sheetName, accountId, agentId, elapsed);
                return;
            }

            // Collect batch update data for rows that match the filter
            var batchData = new List<ValueRange>();

            for (var rowIndex = 1; rowIndex < response.Values.Count; rowIndex++)
            {
                var row = response.Values[rowIndex];
                var cellValue = filterColIndex < row.Count ? row[filterColIndex]?.ToString() ?? string.Empty : string.Empty;

                if (cellValue != filterValue)
                    continue;

                // Redact all cells in the matching row
                var redactedRow = new List<object>(new object[row.Count]);
                for (var i = 0; i < row.Count; i++)
                    redactedRow[i] = redactedMarker;

                var rowRange = $"{sheetName}!A{rowIndex + 1}";
                batchData.Add(new ValueRange
                {
                    Range = rowRange,
                    Values = [redactedRow]
                });
            }

            if (batchData.Count > 0)
            {
                var batchRequest = service.Spreadsheets.Values.BatchUpdate(
                    new BatchUpdateValuesRequest
                    {
                        Data = batchData,
                        ValueInputOption = "USER_ENTERED"
                    },
                    spreadsheetId);

                await batchRequest.ExecuteAsync(ct);
            }

            var durationMs = Stopwatch.GetElapsedTime(sw).TotalMilliseconds;
            GSheetsDiagnostics.Operations.Add(1);
            GSheetsDiagnostics.Duration.Record(durationMs);

            logger.LogInformation(
                "[GSHEETS-003] Redacted {Count} rows for account {AccountId}, agent {AgentId}, sheet {SheetName}. Duration: {Duration}ms",
                batchData.Count, accountId, agentId, sheetName, durationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GSheetsDiagnostics.Failed.Add(1);
            logger.LogError(ex,
                "[GSHEETS-033] Sheets redact failed for account {AccountId}, agent {AgentId}, sheet {SheetName}",
                accountId, agentId, sheetName);
            throw;
        }
    }

    private async Task<SheetsService?> BuildServiceAsync(
        string accountId,
        string agentId,
        CancellationToken ct)
    {
        var credential = await refresher.GetValidCredentialAsync(accountId, agentId, ct);
        if (credential is null)
        {
            logger.LogWarning(
                "[GSHEETS-010] No valid token for account {AccountId}, agent {AgentId}. Skipping operation.",
                accountId, agentId);
            GSheetsDiagnostics.TokenMissing.Add(1);
            return null;
        }

        return BuildSheetsService(credential);
    }

    private SheetsService BuildSheetsService(Domain.Shared.Models.OAuthCredential credential) =>
        new(RealEstateStar.Clients.GoogleOAuth.GoogleCredentialFactory.BuildInitializer(credential, clientId, clientSecret));
}
