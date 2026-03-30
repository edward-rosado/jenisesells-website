using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.Interfaces;

public interface IAgentConfigService
{
    /// <summary>
    /// Generates account.json and content.json from gathered activation data.
    /// Does NOT overwrite existing configs.
    /// </summary>
    Task GenerateAsync(
        string accountId,
        string agentId,
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct);
}
