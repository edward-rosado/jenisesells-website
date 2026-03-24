namespace RealEstateStar.Domain.Shared.Interfaces.Storage;

public interface ISheetStorageProvider
{
    Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct);
    Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct);
    Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct);
}
