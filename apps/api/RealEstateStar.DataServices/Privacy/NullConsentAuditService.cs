namespace RealEstateStar.DataServices.Privacy;

/// <summary>No-op consent audit service used when Azure Table Storage is not configured.</summary>
public sealed class NullConsentAuditService : IConsentAuditService
{
    public Task RecordAsync(string agentId, MarketingConsent consent, string hmacSignature, CancellationToken ct) =>
        Task.CompletedTask;
}
