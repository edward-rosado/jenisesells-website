namespace RealEstateStar.Domain.Shared.Interfaces.External;

public interface IGmailSender
{
    Task SendAsync(string accountId, string agentId, string to, string subject,
        string htmlBody, CancellationToken ct);

    Task SendWithAttachmentAsync(string accountId, string agentId, string to, string subject,
        string htmlBody, byte[] attachmentBytes, string fileName, CancellationToken ct);
}
