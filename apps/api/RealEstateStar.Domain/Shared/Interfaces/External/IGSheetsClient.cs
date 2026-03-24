namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IGSheetsClient
{
    Task AppendRowAsync(string accountId, string agentId, string spreadsheetId, string sheetName, List<string> values, CancellationToken ct);
    Task<List<List<string>>> ReadRowsAsync(string accountId, string agentId, string spreadsheetId, string sheetName, CancellationToken ct);
    Task RedactRowsAsync(string accountId, string agentId, string spreadsheetId, string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct);
}
