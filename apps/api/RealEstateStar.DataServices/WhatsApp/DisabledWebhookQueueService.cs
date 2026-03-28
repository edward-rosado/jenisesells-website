using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.DataServices.WhatsApp;

/// <summary>
/// Null-object implementation registered when WhatsApp is not configured.
/// Ensures the DI container resolves IWebhookQueueService without throwing.
/// </summary>
public class DisabledWebhookQueueService(ILogger<DisabledWebhookQueueService> logger) : IWebhookQueueService
{
    public Task EnqueueAsync(WebhookEnvelope envelope, CancellationToken ct)
    {
        logger.LogDebug("[WA-001] WhatsApp disabled — skipping enqueue for {MessageId}", envelope.MessageId);
        return Task.CompletedTask;
    }

    public Task<QueuedMessage<WebhookEnvelope>?> DequeueAsync(CancellationToken ct) =>
        Task.FromResult<QueuedMessage<WebhookEnvelope>?>(null);

    public Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct) =>
        Task.CompletedTask;
}
