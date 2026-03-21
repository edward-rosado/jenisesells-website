using RealEstateStar.Domain.WhatsApp.Interfaces;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.WhatsApp;

/// <summary>
/// Null-object implementation registered when WhatsApp is not configured.
/// Ensures the DI container resolves IWhatsAppAuditService without throwing.
/// </summary>
public class DisabledWhatsAppAuditService(ILogger<DisabledWhatsAppAuditService> logger) : IWhatsAppAuditService
{
    public Task RecordReceivedAsync(string messageId, string fromPhone,
        string toPhoneNumberId, string body, string messageType, CancellationToken ct)
    {
        logger.LogDebug("[WA-000] WhatsApp disabled — skipping audit RecordReceived for {MessageId}", messageId);
        return Task.CompletedTask;
    }

    public Task UpdateProcessingAsync(string messageId, string agentId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task UpdateCompletedAsync(string messageId, string agentId,
        string intent, string response, CancellationToken ct) =>
        Task.CompletedTask;

    public Task UpdateFailedAsync(string messageId, string? agentId,
        string error, CancellationToken ct) =>
        Task.CompletedTask;

    public Task UpdatePoisonAsync(string messageId, string error, CancellationToken ct) =>
        Task.CompletedTask;
}
