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

    /// <summary>
    /// What each settled document was booked at, keyed by (type code, id). A
    /// document not in here answers null — nothing posted against it — which is
    /// the ordinary case for every test that is not about exchange differences.
    /// </summary>
    public Dictionary<(string, long), SettlementRate> SettlementRates { get; } = [];

    /// <summary>Set to make the rate lookup fail the way an unreachable Accounting does.</summary>
    public bool SettlementRateUnavailable { get; set; }

    public Task<SettlementRate?> SettlementRateAsync(
        string transactionTypeCode, long transactionId, CancellationToken ct)
    {
        if (SettlementRateUnavailable)
        {
            throw new SettlementRateUnavailableException();
        }

        return Task.FromResult(
            SettlementRates.TryGetValue(
                (transactionTypeCode.ToUpperInvariant(), transactionId), out SettlementRate? rate)
                ? rate
                : null);
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
