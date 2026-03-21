using RealEstateStar.Domain.WhatsApp.Interfaces;
using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.DataServices.WhatsApp;

public class AzureWebhookQueueService(
    QueueClient queueClient,
    ILogger<AzureWebhookQueueService> logger) : IWebhookQueueService
{
    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromSeconds(30);

    public async Task EnqueueAsync(WebhookEnvelope envelope, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(envelope);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        await queueClient.SendMessageAsync(base64, cancellationToken: ct);
        logger.LogInformation("[WA-018] Enqueued message {MessageId}", envelope.MessageId);
    }

    public async Task<QueuedMessage<WebhookEnvelope>?> DequeueAsync(CancellationToken ct)
    {
        var response = await queueClient.ReceiveMessageAsync(VisibilityTimeout, ct);
        var msg = response.Value;
        if (msg is null) return null;

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(msg.Body.ToString()));
        var envelope = JsonSerializer.Deserialize<WebhookEnvelope>(json)!;

        return new QueuedMessage<WebhookEnvelope>(envelope, msg.MessageId, msg.PopReceipt,
            msg.DequeueCount);
    }

    public async Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        await queueClient.DeleteMessageAsync(messageId, popReceipt, ct);
        logger.LogInformation("[WA-019] Completed message {MessageId}", messageId);
    }
}
