using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Notification.Worker.Persistence;

public enum ClaimOutcome
{
    /// <summary>This worker holds the message now and may send it.</summary>
    Claimed,

    /// <summary>Already sent. Complete it and send nothing.</summary>
    AlreadySent,

    /// <summary>Another delivery is sending it right now. Leave it for later.</summary>
    InProgress,
}

/// <summary>Which messages have been sent, so a redelivered one sends nothing (TK-19).</summary>
public interface IProcessedMessageStore
{
    Task<ClaimOutcome> TryClaimAsync(string messageId, string topic, CancellationToken ct);

    Task MarkSentAsync(string messageId, CancellationToken ct);

    /// <summary>Gives a claim back after a failed send, so the next delivery sends it.</summary>
    Task ReleaseAsync(string messageId, CancellationToken ct);
}

/// <summary>
/// The claims in <c>ntf.ProcessedMessages</c>.
///
/// <b>Claimed before sending, marked after.</b> Marking only after a send leaves
/// a window where two deliveries both send; claiming first closes it — the
/// primary key lets exactly one insert through. A claim whose send died holds
/// the message for <see cref="StaleAfter"/>, after which the next delivery takes
/// it over with a guarded update whose row count is the answer, so two cannot
/// both take it over. The one duplicate this cannot prevent is a crash between
/// the SMTP server accepting the mail and the row being marked — for one-time
/// codes and invitations a second copy is the better failure than none.
/// </summary>
public sealed class ProcessedMessageStore : IProcessedMessageStore
{
    /// <summary>How long a claim with no send behind it holds a message.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    private readonly NotificationDbContext _db;
    private readonly TimeProvider _clock;

    public ProcessedMessageStore(NotificationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ClaimOutcome> TryClaimAsync(string messageId, string topic, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();

        ProcessedMessage? existing = await _db.ProcessedMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MessageId == messageId, ct);

        if (existing is null)
        {
            _db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                Topic = topic,
                ClaimedAt = now,
            });

            try
            {
                await _db.SaveChangesAsync(ct);
                return ClaimOutcome.Claimed;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Another delivery inserted it between the read and the write.
                _db.ChangeTracker.Clear();
                return ClaimOutcome.InProgress;
            }
        }

        if (existing.SentAt is not null)
        {
            return ClaimOutcome.AlreadySent;
        }

        if (existing.ClaimedAt > now - StaleAfter)
        {
            return ClaimOutcome.InProgress;
        }

        int taken = await _db.ProcessedMessages
            .Where(m => m.MessageId == messageId && m.SentAt == null && m.ClaimedAt == existing.ClaimedAt)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.ClaimedAt, now), ct);

        return taken == 1 ? ClaimOutcome.Claimed : ClaimOutcome.InProgress;
    }

    public Task MarkSentAsync(string messageId, CancellationToken ct) =>
        _db.ProcessedMessages
            .Where(m => m.MessageId == messageId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.SentAt, _clock.GetUtcNow()), ct);

    public Task ReleaseAsync(string messageId, CancellationToken ct) =>
        _db.ProcessedMessages
            .Where(m => m.MessageId == messageId && m.SentAt == null)
            .ExecuteDeleteAsync(ct);
}
