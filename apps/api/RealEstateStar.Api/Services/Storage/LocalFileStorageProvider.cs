using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Services.Storage;

/// <summary>
/// File-based implementation of IFileStorageProvider that writes to local disk.
/// Used for local development and as a fallback when Google Drive is unavailable.
/// Files are written to {basePath}/{folder}/{fileName}.
/// Spreadsheet operations use CSV files in {basePath}/sheets/{sheetName}.csv.
/// </summary>
public class LocalFileStorageProvider(
    string basePath,
    ILogger<LocalFileStorageProvider> logger) : IFileStorageProvider
{
    public async Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        var path = GetDocPath(folder, fileName);
        EnsureDirectory(path);
        await File.WriteAllTextAsync(path, content, ct);
        logger.LogInformation("[STORAGE-LOCAL-001] Wrote {Path}", path);
    }

    public async Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        var path = GetDocPath(folder, fileName);
        if (!File.Exists(path))
            return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    public async Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        var path = GetDocPath(folder, fileName);
        EnsureDirectory(path);

        // Read existing content and append (update = append for conversation logs)
        var existing = File.Exists(path)
            ? await File.ReadAllTextAsync(path, ct)
            : "";
        await File.WriteAllTextAsync(path, existing + content, ct);
        logger.LogInformation("[STORAGE-LOCAL-002] Updated {Path}", path);
    }

    public Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        var path = GetDocPath(folder, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            logger.LogInformation("[STORAGE-LOCAL-003] Deleted {Path}", path);
        }
        return Task.CompletedTask;
    }

    public Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
    {
        var dir = GetFolderPath(folder);
        if (!Directory.Exists(dir))
            return Task.FromResult(new List<string>());

        var files = Directory.GetFiles(dir)
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .Cast<string>()
            .ToList();
        return Task.FromResult(files);
    }

    public async Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct)
    {
        var path = GetSheetPath(sheetName);
        EnsureDirectory(path);
        var csvLine = string.Join(",", values.Select(EscapeCsv));
        await File.AppendAllTextAsync(path, csvLine + Environment.NewLine, ct);
        logger.LogInformation("[STORAGE-LOCAL-004] Appended row to {Path}", path);
    }

    public async Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn,
        string filterValue, CancellationToken ct)
    {
        var path = GetSheetPath(sheetName);
        if (!File.Exists(path))
            return [];

        var lines = await File.ReadAllLinesAsync(path, ct);
        if (lines.Length == 0)
            return [];

        // First line is header — find column index
        var headers = ParseCsvLine(lines[0]);
        var colIndex = headers.IndexOf(filterColumn);
        if (colIndex < 0)
            return [];

        return lines.Skip(1)
            .Select(ParseCsvLine)
            .Where(row => colIndex < row.Count && row[colIndex] == filterValue)
            .ToList();
    }

    public async Task RedactRowsAsync(string sheetName, string filterColumn,
        string filterValue, string redactedMarker, CancellationToken ct)
    {
        var path = GetSheetPath(sheetName);
        if (!File.Exists(path))
            return;

        var lines = await File.ReadAllLinesAsync(path, ct);
        if (lines.Length == 0)
            return;

        var headers = ParseCsvLine(lines[0]);
        var colIndex = headers.IndexOf(filterColumn);
        if (colIndex < 0)
            return;

        var result = new List<string> { lines[0] }; // Keep header
        foreach (var line in lines.Skip(1))
        {
            var row = ParseCsvLine(line);
            if (colIndex < row.Count && row[colIndex] == filterValue)
            {
                // Replace all values except the filter column with redacted marker
                for (var i = 0; i < row.Count; i++)
                    if (i != colIndex) row[i] = redactedMarker;
            }
            result.Add(string.Join(",", row.Select(EscapeCsv)));
        }

        await File.WriteAllLinesAsync(path, result, ct);
        logger.LogInformation("[STORAGE-LOCAL-005] Redacted rows in {Path}", path);
    }

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
    {
        var dir = GetFolderPath(folder);
        Directory.CreateDirectory(dir);
        return Task.CompletedTask;
    }

    // ── Path helpers ──────────────────────────────────────────────────────────

    private string GetDocPath(string folder, string fileName)
    {
        var sanitizedFolder = SanitizePath(folder);
        var sanitizedFile = SanitizePath(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, sanitizedFolder, sanitizedFile));

        // Path traversal protection
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path traversal detected: {folder}/{fileName}");

        return fullPath;
    }

    private string GetFolderPath(string folder)
    {
        var sanitized = SanitizePath(folder);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, sanitized));

        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path traversal detected: {folder}");

        return fullPath;
    }

    private string GetSheetPath(string sheetName)
        => GetDocPath("sheets", SanitizePath(sheetName) + ".csv");

    private static string SanitizePath(string input)
        => input.Replace("..", "").Replace("~", "");

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
    }

    // ── CSV helpers ───────────────────────────────────────────────────────────

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
