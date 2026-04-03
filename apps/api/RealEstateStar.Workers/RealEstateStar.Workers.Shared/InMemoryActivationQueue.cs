using System.Threading.Channels;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// In-memory activation queue for local development. Messages do not survive restarts.
/// Implements <see cref="IActivationQueue"/> using an unbounded Channel.
/// </summary>
public sealed class InMemoryActivationQueue : IActivationQueue
{
    private readonly Channel<ActivationRequest> _channel = Channel.CreateUnbounded<ActivationRequest>();

    public int QueueDepth => _channel.Reader.Count;

    public async Task EnqueueAsync(ActivationRequest request, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(request, ct);
    }

    public async Task<QueueMessage<ActivationRequest>?> DequeueAsync(
        TimeSpan visibilityTimeout,
        CancellationToken ct)
    {
        if (!await _channel.Reader.WaitToReadAsync(ct))
            return null;

        if (_channel.Reader.TryRead(out var request))
            return new QueueMessage<ActivationRequest>(request, Guid.NewGuid().ToString(), string.Empty);

        return null;
    }

    public Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct)
    {
        // No-op for in-memory: message was already consumed on dequeue
        return Task.CompletedTask;
    }
}
