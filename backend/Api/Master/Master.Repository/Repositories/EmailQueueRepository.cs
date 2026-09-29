using Master.Repository.Clients;
using System.Threading.Channels;
using Shared.Kernel.Interfaces;

namespace Master.Repository.Repositories;

public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default);

    ValueTask<EmailMessage> DequeueAsync(CancellationToken ct);
}
