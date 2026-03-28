using System.Threading.Channels;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// Base class for bounded processing channels. Provides a common Channel&lt;T&gt; pattern
/// with configurable capacity and backpressure via <see cref="BoundedChannelFullMode.Wait"/>.
/// </summary>
public abstract class ProcessingChannelBase<T>
{
    private readonly Channel<T> _channel;

    protected ProcessingChannelBase(int capacity)
    {
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelWriter<T> Writer => _channel.Writer;
    public ChannelReader<T> Reader => _channel.Reader;
    public int Count => _channel.Reader.Count;
}
