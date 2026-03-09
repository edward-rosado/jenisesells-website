using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services;

public interface IAgentConfigService
{
    Task<AgentConfig?> GetAgentAsync(string agentId, CancellationToken ct = default);
}
