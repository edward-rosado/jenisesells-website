using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services;

public class AgentConfigService(string configDirectory, ILogger<AgentConfigService>? logger = null) : IAgentConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public async Task<AgentConfig?> GetAgentAsync(string agentId)
    {
        var filePath = Path.Combine(configDirectory, $"{agentId}.json");

        if (!File.Exists(filePath))
        {
            logger?.LogWarning("Agent config file not found: {FilePath}", filePath);
            return null;
        }

        logger?.LogInformation("Loading agent config from {FilePath}", filePath);

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AgentConfig>(stream, JsonOptions);
    }
}
