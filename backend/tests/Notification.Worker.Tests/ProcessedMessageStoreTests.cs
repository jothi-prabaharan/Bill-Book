using Microsoft.EntityFrameworkCore;
using Notification.Worker.Persistence;
using Xunit;

namespace Notification.Worker.Tests;

/// <summary>
/// The claims that make a redelivered email send once (TK-19).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProcessedMessageStoreTests
{
    private readonly PostgresFixture _postgres;

    public ProcessedMessageStoreTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A store on its own context, as one delivery in its own scope would have.</summary>
    private ProcessedMessageStore Store(Clock clock) => new(_postgres.CreateContext(), clock);

    private static string NewId() => Guid.NewGuid().ToString("N");

    [SkippableFact]
    public async Task The_first_delivery_claims_a_message_and_a_second_is_told_it_is_in_progress()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var clock = new Clock();
        string id = NewId();

        Assert.Equal(ClaimOutcome.Claimed, await Store(clock).TryClaimAsync(id, "EmailRequested", default));
        Assert.Equal(ClaimOutcome.InProgress, await Store(clock).TryClaimAsync(id, "EmailRequested", default));
    }

    [SkippableFact]
    public async Task A_message_marked_sent_is_a_duplicate_ever_after()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var clock = new Clock();
        string id = NewId();

        await Store(clock).TryClaimAsync(id, "EmailRequested", default);
        await Store(clock).MarkSentAsync(id, default);

        // Even long after: a sent message is never sent again.
        clock.Now = clock.Now.AddDays(3);
        Assert.Equal(ClaimOutcome.AlreadySent, await Store(clock).TryClaimAsync(id, "EmailRequested", default));
    }

    [SkippableFact]
    public async Task A_released_claim_can_be_taken_again_straight_away()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var clock = new Clock();
        string id = NewId();

        await Store(clock).TryClaimAsync(id, "EmailRequested", default);
        await Store(clock).ReleaseAsync(id, default);

        Assert.Equal(ClaimOutcome.Claimed, await Store(clock).TryClaimAsync(id, "EmailRequested", default));
    }

    [SkippableFact]
    public async Task A_claim_whose_send_died_is_taken_over_once_it_is_stale_and_by_one_delivery_only()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var clock = new Clock();
        string id = NewId();

        await Store(clock).TryClaimAsync(id, "EmailRequested", default);

        clock.Now = clock.Now + ProcessedMessageStore.StaleAfter - TimeSpan.FromSeconds(1);
        Assert.Equal(ClaimOutcome.InProgress, await Store(clock).TryClaimAsync(id, "EmailRequested", default));

        clock.Now = clock.Now.AddSeconds(2);
        ClaimOutcome[] racers = await Task.WhenAll(
            Store(clock).TryClaimAsync(id, "EmailRequested", default),
            Store(clock).TryClaimAsync(id, "EmailRequested", default));

        Assert.Single(racers, r => r == ClaimOutcome.Claimed);
    }

    [SkippableFact]
    public async Task Releasing_never_undoes_a_send()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var clock = new Clock();
        string id = NewId();

        await Store(clock).TryClaimAsync(id, "EmailRequested", default);
        await Store(clock).MarkSentAsync(id, default);
        await Store(clock).ReleaseAsync(id, default);

        await using NotificationDbContext db = _postgres.CreateContext();
        Assert.NotNull((await db.ProcessedMessages.SingleAsync(m => m.MessageId == id)).SentAt);
    }
}
