using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Services;

public interface IAgentConfigService
{
    Task<AgentConfig?> GetAgentAsync(string agentId, CancellationToken ct);
}
