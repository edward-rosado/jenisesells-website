using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Services;

public interface IAgentConfigService
{
    Task<AgentConfig?> GetAgentAsync(string agentId, CancellationToken ct);
    Task UpdateAgentAsync(string agentId, AgentConfig config, CancellationToken ct);
    Task<List<string>> GetAllAgentIdsAsync(CancellationToken ct);
}
