using Gateway.Entity.TableEntities;

namespace Gateway.Logging;

/// <summary>
/// Hand-off between the request path and the background writer.
///
/// <see cref="TryWrite"/> is deliberately non-blocking and returns false rather
/// than waiting: a slow or unavailable database must never add latency to a
/// proxied request, so a full queue drops the log entry instead.
/// </summary>
public interface IRequestLogQueue
{
    bool TryWrite(RequestLog entry);

    IAsyncEnumerable<RequestLog> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>Entries dropped because the queue was full, since process start.</summary>
    long DroppedCount { get; }
}
