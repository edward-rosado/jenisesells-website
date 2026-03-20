namespace RealEstateStar.Api.Features.WhatsApp.Services;

public record WebhookEnvelope(
    string MessageId,
    string FromPhone,
    string Body,
    DateTime ReceivedAt,
    string? PhoneNumberId = null,
    string? TraceId = null);

public record QueuedMessage<T>(T Value, string QueueMessageId, string PopReceipt, long DequeueCount);

public interface IWebhookQueueService
{
    Task EnqueueAsync(WebhookEnvelope envelope, CancellationToken ct);
    Task<QueuedMessage<WebhookEnvelope>?> DequeueAsync(CancellationToken ct);
    Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct);
}
