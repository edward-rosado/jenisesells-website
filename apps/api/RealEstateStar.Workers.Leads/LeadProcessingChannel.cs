using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared;

namespace RealEstateStar.Workers.Leads;

/// <summary>
/// Bounded channel for lead processing requests.
/// The endpoint enqueues; the <see cref="LeadProcessingWorker"/> dequeues and processes.
/// Capacity of 100 provides backpressure — if the worker falls behind,
/// the endpoint blocks briefly rather than flooding the thread pool.
/// </summary>
public sealed class LeadProcessingChannel : ProcessingChannelBase<LeadProcessingRequest>
{
    public LeadProcessingChannel() : base(100) { }
}

/// <summary>
/// Immutable request for background lead processing.
/// Captures all context needed so the worker doesn't touch HttpContext.
/// </summary>
public sealed record LeadProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);
