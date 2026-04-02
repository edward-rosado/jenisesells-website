using System.Threading.Channels;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// In-memory lead orchestration queue for local development. Messages do not survive restarts.
/// Implements <see cref="ILeadOrchestrationQueue"/> using an unbounded Channel.
/// </summary>
public sealed class InMemoryLeadOrchestrationQueue : ILeadOrchestrationQueue
{
    private readonly Channel<LeadOrchestrationMessage> _channel = Channel.CreateUnbounded<LeadOrchestrationMessage>();

    public int QueueDepth => _channel.Reader.Count;

    public async Task EnqueueAsync(LeadOrchestrationMessage message, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(message, ct);
    }

    public async Task<QueueMessage<LeadOrchestrationMessage>?> DequeueAsync(
        TimeSpan visibilityTimeout,
        CancellationToken ct)
    {
        if (!await _channel.Reader.WaitToReadAsync(ct))
            return null;

        if (_channel.Reader.TryRead(out var message))
            return new QueueMessage<LeadOrchestrationMessage>(message, Guid.NewGuid().ToString(), string.Empty);

        return null;
    }

    public Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        // No-op for in-memory: message was already consumed on dequeue
        return Task.CompletedTask;
    }
}
