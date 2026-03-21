namespace RealEstateStar.Domain.WhatsApp.Interfaces;

public interface IWhatsAppAuditService
{
    Task RecordReceivedAsync(string messageId, string fromPhone, string toPhoneNumberId,
        string body, string messageType, CancellationToken ct);
    Task UpdateProcessingAsync(string messageId, string agentId, CancellationToken ct);
    Task UpdateCompletedAsync(string messageId, string agentId, string intent,
        string response, CancellationToken ct);
    Task UpdateFailedAsync(string messageId, string? agentId, string error,
        CancellationToken ct);
    Task UpdatePoisonAsync(string messageId, string error, CancellationToken ct);
}
