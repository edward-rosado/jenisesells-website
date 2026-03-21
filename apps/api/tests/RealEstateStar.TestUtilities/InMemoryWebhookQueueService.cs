using System.Collections.Concurrent;
using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.TestUtilities;

public class InMemoryWebhookQueueService : IWebhookQueueService
{
    private readonly ConcurrentQueue<(WebhookEnvelope Envelope, int DequeueCount)> _queue = new();
    public List<WebhookEnvelope> Enqueued { get; } = [];
    public List<string> Completed { get; } = [];

    public Task EnqueueAsync(WebhookEnvelope envelope, CancellationToken ct)
    {
        Enqueued.Add(envelope);
        _queue.Enqueue((envelope, 0));
        return Task.CompletedTask;
    }

    public Task<QueuedMessage<WebhookEnvelope>?> DequeueAsync(CancellationToken ct)
    {
        if (_queue.TryDequeue(out var item))
        {
            var count = item.DequeueCount + 1;
            return Task.FromResult<QueuedMessage<WebhookEnvelope>?>(
                new QueuedMessage<WebhookEnvelope>(item.Envelope,
                    Guid.NewGuid().ToString(), "pop", count));
        }
        return Task.FromResult<QueuedMessage<WebhookEnvelope>?>(null);
    }

    public Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        Completed.Add(messageId);
        return Task.CompletedTask;
    }
}
