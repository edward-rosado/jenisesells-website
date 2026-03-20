using System.Threading.Channels;

namespace RealEstateStar.Api.Features.Leads.Services;

public sealed class HomeSearchProcessingChannel
{
    private readonly Channel<HomeSearchProcessingRequest> _channel =
        Channel.CreateBounded<HomeSearchProcessingRequest>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelWriter<HomeSearchProcessingRequest> Writer => _channel.Writer;
    public ChannelReader<HomeSearchProcessingRequest> Reader => _channel.Reader;
}

public sealed record HomeSearchProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);
