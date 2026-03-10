using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public partial class JsonFileSessionStore(string basePath) : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public JsonFileSessionStore()
        : this(Path.Combine(AppContext.BaseDirectory, "data", "sessions")) { }

    public async Task SaveAsync(OnboardingSession session, CancellationToken ct)
    {
        var sem = _locks.GetOrAdd(session.Id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(basePath);
            var path = GetSafePath(session.Id);
            var json = JsonSerializer.Serialize(session, JsonOptions);
            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, json, ct);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken ct)
    {
        var sem = _locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            var path = GetSafePath(sessionId);
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<OnboardingSession>(json, JsonOptions);
        }
        finally
        {
            sem.Release();
        }
    }

    private string GetSafePath(string sessionId)
    {
        if (!SessionIdRegex().IsMatch(sessionId))
            throw new ArgumentException("Invalid session ID format", nameof(sessionId));

        var fullPath = Path.GetFullPath(Path.Combine(basePath, $"{sessionId}.json"));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path traversal detected", nameof(sessionId));

        return fullPath;
    }

    [GeneratedRegex(@"^[a-f0-9]{12}$")]
    private static partial Regex SessionIdRegex();
}
