using System.Threading.Channels;
using Gateway.Entity.TableEntities;

namespace Gateway.Api.Logging;

/// <inheritdoc />
public class RequestLogQueue : IRequestLogQueue
{
    private readonly Channel<RequestLog> _channel;
    private long _dropped;

    public RequestLogQueue(RequestLogOptions options)
    {
        // DropWrite over Wait: the writer never blocks a request thread. Losing a
        // log line is always preferable to slowing down the traffic it describes.
        _channel = Channel.CreateBounded<RequestLog>(
            new BoundedChannelOptions(options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public bool TryWrite(RequestLog entry)
    {
        if (_channel.Writer.TryWrite(entry))
        {
            return true;
        }

        Interlocked.Increment(ref _dropped);
        return false;
    }

    public IAsyncEnumerable<RequestLog> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
