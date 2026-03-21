using System.Threading.Channels;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Workers.Leads;

/// <summary>
/// Bounded channel for lead processing requests.
/// The endpoint enqueues; the <see cref="LeadProcessingWorker"/> dequeues and processes.
/// Capacity of 100 provides backpressure — if the worker falls behind,
/// the endpoint blocks briefly rather than flooding the thread pool.
/// </summary>
public sealed class LeadProcessingChannel
{
    private readonly Channel<LeadProcessingRequest> _channel =
        Channel.CreateBounded<LeadProcessingRequest>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelWriter<LeadProcessingRequest> Writer => _channel.Writer;
    public ChannelReader<LeadProcessingRequest> Reader => _channel.Reader;
    public int Count => _channel.Reader.Count;
}

/// <summary>
/// Immutable request for background lead processing.
/// Captures all context needed so the worker doesn't touch HttpContext.
/// </summary>
public sealed record LeadProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);
