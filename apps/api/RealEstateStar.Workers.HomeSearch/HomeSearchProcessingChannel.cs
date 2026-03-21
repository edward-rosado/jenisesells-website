using System.Threading.Channels;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Workers.HomeSearch;

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
    public int Count => _channel.Reader.Count;
}

public sealed record HomeSearchProcessingRequest(
    string AgentId,
    Lead Lead,
    string CorrelationId);
