using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.Onboarding;

public partial class JsonFileSessionStore(string basePath, ILogger<JsonFileSessionStore> logger) : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public JsonFileSessionStore(ILogger<JsonFileSessionStore> logger)
        : this(Path.Combine(AppContext.BaseDirectory, "data", "sessions"), logger) { }

    public async Task SaveAsync(OnboardingSession session, CancellationToken ct)
    {
        var sem = _locks.GetOrAdd(session.Id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(basePath);
            var path = GetSafePath(session.Id);

            string json;
            try
            {
                json = JsonSerializer.Serialize(session, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "[SESSION-010] Failed to serialize session {SessionId}. " +
                    "State={State}, MessageCount={MessageCount}",
                    session.Id, session.CurrentState, session.Messages.Count);
                throw;
            }

            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, json, ct);
            File.Move(tmp, path, overwrite: true);

            logger.LogDebug("[SESSION-011] Saved session {SessionId} ({Bytes} bytes)", session.Id, json.Length);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "[SESSION-012] I/O error saving session {SessionId} to {BasePath}",
                session.Id, basePath);
            throw;
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
            if (!File.Exists(path))
            {
                logger.LogDebug("[SESSION-013] Session file not found for {SessionId}", sessionId);
                return null;
            }

            var json = await File.ReadAllTextAsync(path, ct);
            logger.LogDebug("[SESSION-014] Read session file for {SessionId} ({Bytes} bytes)", sessionId, json.Length);

            try
            {
                return JsonSerializer.Deserialize<OnboardingSession>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "[SESSION-015] Failed to deserialize session {SessionId}. " +
                    "JSON preview: {Preview}",
                    sessionId, json[..Math.Min(json.Length, 500)]);
                throw;
            }
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "[SESSION-016] I/O error loading session {SessionId} from {BasePath}",
                sessionId, basePath);
            throw;
        }
        finally
        {
            sem.Release();
        }
    }

    internal string GetSafePath(string sessionId)
    {
        if (!SessionIdRegex().IsMatch(sessionId))
        {
            logger.LogWarning("[SESSION-017] Invalid session ID format: {SessionId}", sessionId);
            throw new ArgumentException("Invalid session ID format", nameof(sessionId));
        }

        var fullPath = Path.GetFullPath(Path.Combine(basePath, $"{sessionId}.json"));
        ValidatePathWithinBase(fullPath, Path.GetFullPath(basePath), sessionId);

        return fullPath;
    }

    /// <summary>
    /// Defense-in-depth: verifies the resolved file path is within the base directory.
    /// The regex on session IDs prevents path traversal, but this check catches
    /// any edge cases in path normalization.
    /// </summary>
    internal void ValidatePathWithinBase(string fullPath, string resolvedBasePath, string sessionId)
    {
        if (!fullPath.StartsWith(resolvedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[SESSION-018] Path traversal detected for session ID: {SessionId}", sessionId);
            throw new ArgumentException("Path traversal detected", nameof(sessionId));
        }
    }

    [GeneratedRegex(@"^[a-f0-9]{12}$")]
    private static partial Regex SessionIdRegex();
}
