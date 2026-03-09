using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services;

public partial class AgentConfigService(string configDirectory, ILogger<AgentConfigService>? logger = null) : IAgentConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    [GeneratedRegex(@"^[a-z0-9-]+$")]
    private static partial Regex AgentIdPattern();

    public async Task<AgentConfig?> GetAgentAsync(string agentId)
    {
        ValidateAgentId(agentId);

        var filePath = Path.Combine(configDirectory, $"{agentId}.json");
        var resolvedPath = Path.GetFullPath(filePath);
        var resolvedConfigDir = Path.GetFullPath(configDirectory);

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
        return await JsonSerializer.DeserializeAsync<AgentConfig>(stream, JsonOptions);
    }

    private static void ValidateAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId) || !AgentIdPattern().IsMatch(agentId))
            throw new ArgumentException($"Invalid agent id: {agentId}", nameof(agentId));
    }
}
