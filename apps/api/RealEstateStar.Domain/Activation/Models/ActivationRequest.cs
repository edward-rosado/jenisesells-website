namespace RealEstateStar.Domain.Activation.Models;

public sealed record ActivationRequest(
    string AccountId,
    string AgentId,
    string Email,
    DateTime Timestamp,
    ActivationTier Tier = ActivationTier.Mvp);
