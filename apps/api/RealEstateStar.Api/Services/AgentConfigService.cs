using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Services;

public partial class AgentConfigService(string configDirectory, ILogger<AgentConfigService>? logger = null) : IAgentConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    [GeneratedRegex(@"^[a-z0-9-]+$")]
    private static partial Regex AgentIdPattern();

    public async Task<AgentConfig?> GetAgentAsync(string agentId, CancellationToken ct)
    {
        ValidateAgentId(agentId);

        var resolvedConfigDir = Path.GetFullPath(configDirectory);

        // Support both directory format ({id}/config.json) and flat format ({id}.json)
        var directoryPath = Path.GetFullPath(Path.Combine(configDirectory, agentId, "config.json"));
        var flatPath = Path.GetFullPath(Path.Combine(configDirectory, $"{agentId}.json"));
        var resolvedPath = File.Exists(directoryPath) ? directoryPath : flatPath;

        if (!resolvedPath.StartsWith(resolvedConfigDir, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("Path traversal attempt detected for agent id {AgentId}", agentId);
            throw new ArgumentException($"Invalid agent id: {agentId}", nameof(agentId));
        }

        if (!File.Exists(resolvedPath))
        {
            logger?.LogWarning("Agent config file not found: {FilePath}", resolvedPath);
            return null;
        }

        logger?.LogInformation("Loading agent config from {FilePath}", resolvedPath);

        await using var stream = File.OpenRead(resolvedPath);
        return await JsonSerializer.DeserializeAsync<AgentConfig>(stream, JsonOptions, ct);
    }

    public async Task UpdateAgentAsync(string agentId, AgentConfig config, CancellationToken ct)
    {
        ValidateAgentId(agentId);

        var resolvedConfigDir = Path.GetFullPath(configDirectory);

        var directoryPath = Path.GetFullPath(Path.Combine(configDirectory, agentId, "config.json"));
        var flatPath = Path.GetFullPath(Path.Combine(configDirectory, $"{agentId}.json"));
        var resolvedPath = File.Exists(directoryPath) ? directoryPath : flatPath;

        if (!resolvedPath.StartsWith(resolvedConfigDir, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("Path traversal attempt detected for agent id {AgentId}", agentId);
            throw new ArgumentException($"Invalid agent id: {agentId}", nameof(agentId));
        }

        var tempPath = resolvedPath + ".tmp";
        await using (var stream = File.OpenWrite(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, WriteOptions, ct);
        }

        File.Move(tempPath, resolvedPath, overwrite: true);
        logger?.LogInformation("Updated agent config for {AgentId}", agentId);
    }

    public Task<List<string>> GetAllAgentIdsAsync(CancellationToken ct)
    {
        var resolvedConfigDir = Path.GetFullPath(configDirectory);
        var ids = new List<string>();

        if (!Directory.Exists(resolvedConfigDir))
            return Task.FromResult(ids);

        // Flat format: {id}.json
        foreach (var file in Directory.EnumerateFiles(resolvedConfigDir, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (AgentIdPattern().IsMatch(id))
                ids.Add(id);
        }

        // Directory format: {id}/config.json
        foreach (var dir in Directory.EnumerateDirectories(resolvedConfigDir))
        {
            var id = Path.GetFileName(dir);
            if (AgentIdPattern().IsMatch(id) && File.Exists(Path.Combine(dir, "config.json")))
            {
                if (!ids.Contains(id))
                    ids.Add(id);
            }
        }

        return Task.FromResult(ids);
    }

    private static void ValidateAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId) || !AgentIdPattern().IsMatch(agentId))
            throw new ArgumentException($"Invalid agent id: {agentId}", nameof(agentId));
    }
}
