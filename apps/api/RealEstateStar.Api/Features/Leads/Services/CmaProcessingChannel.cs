using System.Threading.Channels;

namespace RealEstateStar.Api.Features.Leads.Services;

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
}

public sealed record CmaProcessingRequest(
    string AgentId,
    Lead Lead,
    LeadEnrichment Enrichment,
    LeadScore Score,
    string CorrelationId);
