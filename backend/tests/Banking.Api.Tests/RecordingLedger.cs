using Banking.Api.Services;

namespace Banking.Api.Tests;

/// <summary>
/// Stands in for Accounting and keeps what it was asked to post.
///
/// The point of these tests is <b>what Banking describes</b> — which accounts,
/// which sub-account purposes, which direction — not whether Accounting writes
/// it, which Accounting's own suite covers. So the ledger is recorded rather
/// than mocked away: the assertions read the legs.
/// </summary>
public sealed class RecordingLedger : IAccountingLedger
{
    private readonly DateOnly? _lockedUpto;
    private readonly bool _lockUnavailable;

    public RecordingLedger(DateOnly? lockedUpto = null, bool lockUnavailable = false)
    {
        _lockedUpto = lockedUpto;
        _lockUnavailable = lockUnavailable;
    }

    /// <summary>Every posting handed over, newest last.</summary>
    public List<LedgerPosting> Postings { get; } = [];

    /// <summary>What the next posting attempt returns. Set to test a refusal.</summary>
    public LedgerPostOutcome Outcome { get; set; } = LedgerPostOutcome.Posted;

    public LedgerPosting Last => Postings[^1];

    public Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
    {
        if (Outcome == LedgerPostOutcome.Posted)
        {
            Postings.Add(posting);
        }

        return Task.FromResult(Outcome);
    }

    public Task<DateOnly?> LockedUptoAsync(CancellationToken ct) =>
        _lockUnavailable
            ? throw new PeriodLockUnavailableException()
            : Task.FromResult(_lockedUpto);

    public Task<LedgerAccount?> ProvisionAsync(
        long bankAccountId, string accountName, string accountType, string? currencyCode,
        CancellationToken ct) =>
        Task.FromResult<LedgerAccount?>(null);

    public Task<bool> UpdateAsync(
        long bankAccountId, string accountName, bool isActive, CancellationToken ct) =>
        Task.FromResult(true);
}
