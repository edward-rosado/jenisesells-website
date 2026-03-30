using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.Interfaces;

public interface IAgentContextLoader
{
    Task<AgentContext?> LoadAsync(string accountId, string agentId, CancellationToken ct);
}
