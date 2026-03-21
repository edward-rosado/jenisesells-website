using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Leads;

public class LocalStorageProvider(string basePath) : IFileStorageProvider
{
    public async Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        var fullPath = ResolveSafePath(folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var tempPath = fullPath + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    public async Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        var fullPath = ResolveSafePath(folder, fileName);
        if (!File.Exists(fullPath)) return null;
        return await File.ReadAllTextAsync(fullPath, ct);
    }

    public Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
        => WriteDocumentAsync(folder, fileName, content, ct);

    public Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        var fullPath = ResolveSafePath(folder, fileName);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
    {
        var fullPath = ResolveSafeFolderPath(folder);
        if (!Directory.Exists(fullPath)) return Task.FromResult(new List<string>());
        var files = Directory.GetFiles(fullPath).Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList();
        return Task.FromResult(files);
    }

    public async Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct)
    {
        var csvPath = GetCsvPath(sheetName);
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
        var line = string.Join(",", values.Select(v => $"\"{v.Replace("\"", "\"\"")}\""));
        await File.AppendAllTextAsync(csvPath, line + Environment.NewLine, ct);
    }

    public async Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct)
    {
        var csvPath = GetCsvPath(sheetName);
        if (!File.Exists(csvPath)) return [];

        var lines = await File.ReadAllLinesAsync(csvPath, ct);
        return lines
            .Select(ParseCsvLine)
            .Where(row => row.Contains(filterValue))
            .ToList();
    }

    public async Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct)
    {
        var csvPath = GetCsvPath(sheetName);
        if (!File.Exists(csvPath)) return;

        var lines = await File.ReadAllLinesAsync(csvPath, ct);
        var updated = lines.Select(line =>
            line.Contains(filterValue) ? line.Replace(filterValue, redactedMarker) : line
        ).ToArray();
        await File.WriteAllLinesAsync(csvPath, updated, ct);
    }

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
    {
        var fullPath = ResolveSafeFolderPath(folder);
        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveSafePath(string folder, string fileName)
    {
        ValidatePathComponent(folder);
        ValidatePathComponent(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, folder, fileName));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path traversal detected: {folder}/{fileName}");
        return fullPath;
    }

    private string ResolveSafeFolderPath(string folder)
    {
        ValidatePathComponent(folder);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, folder));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path traversal detected: {folder}");
        return fullPath;
    }

    private static void ValidatePathComponent(string component)
    {
        if (component.Contains(".."))
            throw new ArgumentException($"Path component contains '..': {component}");
    }

    private string GetCsvPath(string sheetName)
    {
        ValidatePathComponent(sheetName);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, "logs", $"{sheetName}.csv"));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path traversal detected: {sheetName}");
        return fullPath;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
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
