namespace Gateway.Api.Logging;

/// <summary>Bound from the <c>RequestLog</c> configuration section.</summary>
public class RequestLogOptions
{
    public const string SectionName = "RequestLog";

    /// <summary>Turn logging off entirely without redeploying.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Rows buffered before a flush is forced. The queue is bounded at
    /// <see cref="QueueCapacity"/>; this is only the batch size.
    /// </summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>Longest a row waits in the buffer before being written anyway.</summary>
    public int FlushIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Hard cap on buffered rows. Once full, new entries are dropped rather than
    /// queued: a logging backlog must never become backpressure on real traffic.
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Rows older than this are deleted by the purge job.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often the purge job runs.</summary>
    public int PurgeIntervalHours { get; set; } = 24;

    /// <summary>
    /// Paths that are never logged. Health checks and the status page would
    /// otherwise be most of the table.
    /// </summary>
    public string[] ExcludedPaths { get; set; } = ["/health", "/"];
}
