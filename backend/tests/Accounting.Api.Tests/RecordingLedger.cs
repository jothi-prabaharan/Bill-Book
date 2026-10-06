using Accounting.Api.Services;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;

namespace Accounting.Api.Tests;

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
    private readonly AccountingDbContext? _db;

    public RecordingLedger(DateOnly? lockedUpto = null, bool lockUnavailable = false, AccountingDbContext? db = null)
    {
        _lockedUpto = lockedUpto;
        _lockUnavailable = lockUnavailable;
        _db = db;
    }

    /// <summary>Every posting handed over, newest last.</summary>
    public List<LedgerPosting> Postings { get; } = [];

    /// <summary>What the next posting attempt returns. Set to test a refusal.</summary>
    public LedgerPostOutcome Outcome { get; set; } = LedgerPostOutcome.Posted;

    public LedgerPosting Last => Postings[^1];

    public async Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
    {
        if (Outcome == LedgerPostOutcome.Posted)
        {
            Postings.Add(posting);
            if (_db != null)
            {
                foreach (var leg in posting.Legs)
                {
                    long accountId = leg.AccountId ?? 0;
                    if (accountId == 0 && leg.AccountSystemName != null)
                    {
                        accountId = _db.Accounts
                            .Where(a => a.AccountSystemName == leg.AccountSystemName)
                            .Select(a => a.AccountId)
                            .FirstOrDefault();
                    }

                    if (accountId != 0)
                    {
                        _db.JournalLedger.Add(new JournalLedger
                        {
                            OrgId = _db.CurrentOrgId,
                            LedgerDate = posting.LedgerDate,
                            AccountId = accountId,
                            TransactionTypeCode = posting.TransactionTypeCode,
                            TransactionId = posting.TransactionId,
                            TransactionDetailId = leg.TransactionDetailId,
                            DebitAmount = leg.DebitAmount,
                            CreditAmount = leg.CreditAmount,
                            DebitAmountBase = leg.DebitAmount,
                            CreditAmountBase = leg.CreditAmount,
                            CurrencyCode = posting.CurrencyCode,
                            ExchangeRate = posting.ExchangeRate,
                            ContactId = leg.SubAccountReferenceType == 1 ? (leg.SubAccountReferenceId ?? posting.ContactId) : null,
                            LedgerTypeId = leg.LedgerTypeId,
                            LedgerSourceId = leg.LedgerSourceId,
                            DocumentNo = $"{posting.TransactionTypeCode}/{posting.TransactionId}",
                        });
                    }
                }

                await _db.SaveChangesAsync(ct);
            }
        }

        return Outcome;
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
