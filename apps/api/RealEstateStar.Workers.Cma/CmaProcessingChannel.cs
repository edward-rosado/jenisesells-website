using System.Threading.Channels;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Workers.Cma;

public sealed class CmaProcessingChannel
{
    private readonly Channel<CmaProcessingRequest> _channel =
        Channel.CreateBounded<CmaProcessingRequest>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelWriter<CmaProcessingRequest> Writer => _channel.Writer;
    public ChannelReader<CmaProcessingRequest> Reader => _channel.Reader;
    public int Count => _channel.Reader.Count;
}

public sealed record CmaProcessingRequest(
    string AgentId,
    Lead Lead,
    LeadEnrichment Enrichment,
    LeadScore Score,
    string CorrelationId);
