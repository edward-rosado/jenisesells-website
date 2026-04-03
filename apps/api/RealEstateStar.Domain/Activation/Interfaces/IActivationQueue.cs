using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Activation.Interfaces;

/// <summary>
/// Durable queue for activation requests. Messages survive container restarts.
/// </summary>
public interface IActivationQueue
{
    /// <summary>
    /// Returns the approximate number of messages currently in the queue.
    /// For Azure Queue Storage this is eventually consistent; for in-memory it is exact.
    /// </summary>
    int QueueDepth { get; }

    Task EnqueueAsync(ActivationRequest request, CancellationToken ct);
    Task<QueueMessage<ActivationRequest>?> DequeueAsync(TimeSpan visibilityTimeout, CancellationToken ct);
    Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct);
}

/// <summary>
/// Wraps a dequeued message with the metadata needed to complete/delete it.
/// </summary>
public sealed record QueueMessage<T>(T Value, string MessageId, string PopReceipt);
