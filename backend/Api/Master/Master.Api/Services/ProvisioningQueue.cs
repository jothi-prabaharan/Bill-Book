using System.Threading.Channels;

namespace Master.Api.Services;

/// <summary>One tenant-provisioning request, queued at signup.</summary>
public sealed record ProvisioningJob(
    Guid CustomerId,
    Guid OrgId,
    string DatabaseName,
    string OwnerEmail,
    string OwnerDisplayName,
    string? OwnerMobileNumber,
    string OwnerPassword);

public interface IProvisioningQueue
{
    ValueTask EnqueueAsync(ProvisioningJob job, CancellationToken ct = default);

    ValueTask<ProvisioningJob> DequeueAsync(CancellationToken ct);
}

/// <summary>
/// In-process channel-backed queue. Survives within the process only — if the
/// service dies mid-provision the customer stays in Provisioning/Failed and the
/// platform admin retries. A durable queue (Service Bus) can replace this
/// behind the same interface later.
/// </summary>
public sealed class InProcessProvisioningQueue : IProvisioningQueue
{
    private readonly Channel<ProvisioningJob> _channel =
        Channel.CreateUnbounded<ProvisioningJob>();

    public ValueTask EnqueueAsync(ProvisioningJob job, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(job, ct);

    public ValueTask<ProvisioningJob> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
