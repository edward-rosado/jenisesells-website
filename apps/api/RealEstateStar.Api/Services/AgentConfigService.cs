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

    public async Task<List<AgentConfig>> ListAllAsync(CancellationToken ct)
    {
        var results = new List<AgentConfig>();

        if (!Directory.Exists(configDirectory))
            return results;

        // Flat format: {configDirectory}/{id}.json
        var jsonFiles = Directory.GetFiles(configDirectory, "*.json", SearchOption.TopDirectoryOnly);
        foreach (var file in jsonFiles)
        {
            var agentId = Path.GetFileNameWithoutExtension(file);
            if (!AgentIdPattern().IsMatch(agentId))
                continue;

            try
            {
                await using var stream = File.OpenRead(file);
                var config = await JsonSerializer.DeserializeAsync<AgentConfig>(stream, JsonOptions, ct);
                if (config is not null)
                    results.Add(config);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load agent config from {FilePath}", file);
            }
        }

        // Directory format: {configDirectory}/{id}/config.json
        var subDirs = Directory.GetDirectories(configDirectory);
        foreach (var dir in subDirs)
        {
            var agentId = Path.GetFileName(dir);
            if (!AgentIdPattern().IsMatch(agentId))
                continue;

            var configFile = Path.Combine(dir, "config.json");
            if (!File.Exists(configFile))
                continue;

            try
            {
                await using var stream = File.OpenRead(configFile);
                var config = await JsonSerializer.DeserializeAsync<AgentConfig>(stream, JsonOptions, ct);
                if (config is not null)
                    results.Add(config);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load agent config from {FilePath}", configFile);
            }
        }

        return results;
    }

    private static void ValidateAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId) || !AgentIdPattern().IsMatch(agentId))
            throw new ArgumentException($"Invalid agent id: {agentId}", nameof(agentId));
    }
}
