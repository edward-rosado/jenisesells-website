using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IGmailReader
{
    Task<IReadOnlyList<EmailMessage>> GetSentEmailsAsync(
        string accountId, string agentId, int maxResults, CancellationToken ct);
    Task<IReadOnlyList<EmailMessage>> GetInboxEmailsAsync(
        string accountId, string agentId, int maxResults, CancellationToken ct);
}
