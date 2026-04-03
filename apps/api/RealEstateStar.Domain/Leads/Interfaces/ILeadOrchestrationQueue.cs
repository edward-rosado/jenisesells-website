using RealEstateStar.Domain.Activation.Interfaces;

namespace RealEstateStar.Domain.Leads.Interfaces;

/// <summary>
/// Durable queue for lead orchestration requests. Messages survive container restarts.
/// </summary>
public interface ILeadOrchestrationQueue
{
    /// <summary>
    /// Returns the approximate number of messages currently in the queue.
    /// For Azure Queue Storage this is eventually consistent; for in-memory it is exact.
    /// </summary>
    int QueueDepth { get; }

    Task EnqueueAsync(LeadOrchestrationMessage message, CancellationToken ct);
    Task<QueueMessage<LeadOrchestrationMessage>?> DequeueAsync(TimeSpan visibilityTimeout, CancellationToken ct);
    Task CompleteAsync(string messageId, string popReceipt, CancellationToken ct);
}

/// <summary>
/// Serializable lead orchestration message for durable queue storage.
/// Contains the essential data needed to process a lead, not the full Lead object.
/// </summary>
public sealed record LeadOrchestrationMessage(
    string AgentId,
    Guid LeadId,
    string CorrelationId);
