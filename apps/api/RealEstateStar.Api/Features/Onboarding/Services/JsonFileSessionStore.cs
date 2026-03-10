using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class JsonFileSessionStore(string basePath) : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonFileSessionStore()
        : this(Path.Combine(AppContext.BaseDirectory, "data", "sessions")) { }

    public async Task SaveAsync(OnboardingSession session, CancellationToken ct)
    {
        Directory.CreateDirectory(basePath);
        var path = GetPath(session.Id);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public async Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken ct)
    {
        var path = GetPath(sessionId);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<OnboardingSession>(json, JsonOptions);
    }

    private string GetPath(string sessionId)
        => Path.Combine(basePath, $"{sessionId}.json");
}
