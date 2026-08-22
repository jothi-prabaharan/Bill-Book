using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

/// <summary>
/// What the money documents need from the ledger: the GL account behind a bank
/// account, a posting, the period lock, and what a document was booked at.
///
/// This was Banking's HTTP client onto Accounting. The merge made it an
/// interface over four in-process services, and the interface is worth keeping:
/// it is the seam the money-document tests substitute at, and it still marks
/// the line the money documents may not write across — only
/// <see cref="LedgerPostingService"/> inserts ledger rows.
/// </summary>
public interface IAccountingLedger
{
    /// <summary>The GL account id, or null when Accounting could not answer.</summary>
    Task<LedgerAccount?> ProvisionAsync(
        long bankAccountId, string accountName, string accountType, string? currencyCode,
        CancellationToken ct);

    /// <summary>Pushes a rename or a deactivation. The bank account owns the name.</summary>
    Task<bool> UpdateAsync(long bankAccountId, string accountName, bool isActive, CancellationToken ct);

    /// <summary>
    /// Writes a money document's legs to the general ledger. Only
    /// <see cref="LedgerPostingService"/> inserts ledger rows, so this describes
    /// a posting rather than making one.
    /// </summary>
    Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct);

    /// <summary>
    /// How far back the books are closed to this caller, or null when nothing
    /// stops them. Read before posting, so a document dated into a closed period
    /// is refused with the date rather than by a constraint.
    /// </summary>
    Task<DateOnly?> LockedUptoAsync(CancellationToken ct);

    /// <summary>
    /// What a document was booked at, read off its ledger rows, or null when it
    /// has none in this branch. Needed before settling a foreign-currency
    /// document: the balance has to come off at the rate it went on at.
    ///
    /// <b>Null is "no rate on record", not "same rate".</b> A caller treats it as
    /// nothing to adjust — an unposted document has no exchange difference to
    /// recognize, because there is nothing yet to be different from.
    /// </summary>
    Task<SettlementRate?> SettlementRateAsync(
        string transactionTypeCode, long transactionId, CancellationToken ct);
}

/// <summary>What a settled document was booked at.</summary>
public sealed record SettlementRate(string CurrencyCode, decimal ExchangeRate);

/// <summary>A money document's posting, as the money documents describe it.</summary>
public sealed record LedgerPosting(
    string TransactionTypeCode,
    long TransactionId,
    DateOnly LedgerDate,
    string CurrencyCode,
    decimal ExchangeRate,
    long? ContactId,
    IReadOnlyList<LedgerPostingLeg> Legs,

    /// <summary>
    /// Which leg types to clear when <paramref name="Legs"/> is empty. That is
    /// how a posting is withdrawn: with no legs there is nothing to infer the
    /// types from, so a void names them.
    /// </summary>
    IReadOnlyList<int>? WithdrawLedgerTypeIds = null);

/// <summary>
/// One leg. The account is named by system name, except the bank leg which
/// carries the id issued when the bank account's GL account was created.
/// </summary>
public sealed record LedgerPostingLeg(
    int LedgerTypeId,
    int LedgerSourceId,
    long TransactionDetailId,
    string? AccountSystemName,
    long? AccountId,
    int? SubAccountReferenceType,
    long? SubAccountReferenceId,
    int SubAccountPurpose,
    decimal DebitAmount,
    decimal CreditAmount,
    string? TransactionDesc,

    /// <summary>
    /// What this leg is denominated in, when that is not the document's currency.
    /// Null on every leg but the two settlement produces — the leg relieving a
    /// balance booked at an older rate, and the base-currency leg carrying the
    /// difference.
    /// </summary>
    string? CurrencyCode = null,

    decimal? ExchangeRate = null);

/// <summary>What came back from a posting attempt.</summary>
public enum LedgerPostOutcome
{
    Posted = 0,

    /// <summary>Transient — the base currency was briefly unreadable, say.</summary>
    Retry = 1,

    /// <summary>The posting was refused. Retrying unchanged will be refused again.</summary>
    Refused = 2,
}

public sealed record LedgerAccount(long AccountId, string AccountCode);

/// <summary>
/// The in-process implementation. Every method here was an HTTP round trip to
/// this same code while Banking was its own service; the merge turned each into
/// a method call on the service next to it.
///
/// That is not only fewer hops. A posting now runs in the caller's own
/// transaction and against the caller's own tenant context, so a money document
/// and the ledger rows behind it commit or roll back together — where before,
/// the document could save and the posting fail with nothing to undo, and the
/// tenant had to be re-established from a forwarded bearer token on the far side.
/// </summary>
public sealed class InProcessAccountingLedger : IAccountingLedger
{
    private readonly BankLedgerService _bankAccounts;
    private readonly LedgerPostingService _postings;
    private readonly PeriodLockService _locks;
    private readonly LedgerReportService _reports;
    private readonly ITenantContext _tenant;
    private readonly ILogger<InProcessAccountingLedger> _log;

    public InProcessAccountingLedger(
        BankLedgerService bankAccounts,
        LedgerPostingService postings,
        PeriodLockService locks,
        LedgerReportService reports,
        ITenantContext tenant,
        ILogger<InProcessAccountingLedger> log)
    {
        _bankAccounts = bankAccounts;
        _postings = postings;
        _locks = locks;
        _reports = reports;
        _tenant = tenant;
        _log = log;
    }

    public async Task<LedgerAccount?> ProvisionAsync(
        long bankAccountId,
        string accountName,
        string accountType,
        string? currencyCode,
        CancellationToken ct)
    {
        BankLedgerResult result = await _bankAccounts.ProvisionAsync(
            new ProvisionBankAccountRequest
            {
                BankAccountId = bankAccountId,
                AccountName = accountName,
                AccountType = accountType,
                CurrencyCode = currencyCode,
            },
            ct);

        if (result.Outcome != BankLedgerOutcome.Ok || result.AccountId is not long accountId)
        {
            // Null rather than an exception, because the caller's handling has
            // not changed: the bank account is saved and the ledger account is
            // provisioned again on the next write. Idempotent on
            // AccountSystemName, so retrying cannot produce a second account.
            _log.LogWarning(
                "Ledger account for bank account {BankAccountId} was not created: {Outcome}.",
                bankAccountId,
                result.Outcome);

            return null;
        }

        return new LedgerAccount(accountId, result.AccountCode!);
    }

    public async Task<bool> UpdateAsync(
        long bankAccountId, string accountName, bool isActive, CancellationToken ct)
    {
        BankLedgerOutcome outcome =
            await _bankAccounts.UpdateAsync(bankAccountId, accountName, isActive, ct);

        if (outcome != BankLedgerOutcome.Ok)
        {
            _log.LogWarning(
                "Ledger update for bank account {BankAccountId} did not apply: {Outcome}.",
                bankAccountId,
                outcome);
        }

        return outcome == BankLedgerOutcome.Ok;
    }

    public async Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
    {
        // The tenant is the caller's own rather than something carried in the
        // body. Across the wire this had to be re-established from a forwarded
        // token; in process the request never left the scope that resolved it.
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            _log.LogError(
                "Posting {Code}-{Id} has no tenant context.",
                posting.TransactionTypeCode,
                posting.TransactionId);

            return LedgerPostOutcome.Refused;
        }

        var request = new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            TransactionTypeCode = posting.TransactionTypeCode,
            TransactionId = posting.TransactionId,
            LedgerDate = posting.LedgerDate,
            CurrencyCode = posting.CurrencyCode,
            ExchangeRate = posting.ExchangeRate,
            ContactId = posting.ContactId,
            SourceDocumentId = posting.TransactionId,
            WithdrawLedgerTypeIds = [.. posting.WithdrawLedgerTypeIds ?? []],
            Legs =
            [
                .. posting.Legs.Select(l => new LedgerLegRequest
                {
                    LedgerTypeId = l.LedgerTypeId,
                    LedgerSourceId = l.LedgerSourceId,
                    TransactionDetailId = l.TransactionDetailId,
                    AccountSystemName = l.AccountSystemName,
                    AccountId = l.AccountId,
                    SubAccountReferenceType =
                        (SubAccountReferenceType?)l.SubAccountReferenceType,
                    SubAccountReferenceId = l.SubAccountReferenceId,
                    SubAccountPurpose = (SubAccountPurpose)l.SubAccountPurpose,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    CurrencyCode = l.CurrencyCode,
                    ExchangeRate = l.ExchangeRate,
                    TransactionDesc = l.TransactionDesc,
                }),
            ],
        };

        PostLedgerResult result = await _postings.PostAsync(request, ct);

        switch (result.Outcome)
        {
            case PostLedgerOutcome.Ok:
                return LedgerPostOutcome.Posted;

            // The same two the HTTP seam answered 503 and 409 to, and for the
            // same reason: both are fixed by something other than this document
            // changing, so both are worth coming back to rather than parking.
            case PostLedgerOutcome.BaseCurrencyUnavailable:
            case PostLedgerOutcome.AccountMissing:
            case PostLedgerOutcome.SubAccountMissing:
                _log.LogWarning(
                    "Posting {Code}-{Id} deferred: {Outcome}. {Detail}",
                    posting.TransactionTypeCode,
                    posting.TransactionId,
                    result.Outcome,
                    result.Detail);

                return LedgerPostOutcome.Retry;

            default:
                _log.LogError(
                    "Posting {Code}-{Id} was refused: {Outcome}. {Detail}",
                    posting.TransactionTypeCode,
                    posting.TransactionId,
                    result.Outcome,
                    result.Detail);

                return LedgerPostOutcome.Refused;
        }
    }

    public async Task<DateOnly?> LockedUptoAsync(CancellationToken ct)
    {
        try
        {
            return await _locks.LockedUptoAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unreadable is not unlocked. The read is a local query now rather
            // than a call, so this is a database that answered with an error —
            // rarer than a dropped connection was, and no safer to treat as
            // "nothing stops you". The caller still refuses and can retry.
            _log.LogWarning(ex, "Period lock lookup failed.");
            throw new PeriodLockUnavailableException();
        }
    }

    public async Task<SettlementRate?> SettlementRateAsync(
        string transactionTypeCode, long transactionId, CancellationToken ct)
    {
        try
        {
            SettlementRateView? rate =
                await _reports.GetSettlementRateAsync(transactionTypeCode, transactionId, ct);

            // Null stays "no rate on record" — a document with nothing posted
            // against it yet — which the caller reads as nothing to adjust.
            return rate is null ? null : new SettlementRate(rate.CurrencyCode, rate.ExchangeRate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(
                ex, "Settlement rate for {Code}-{Id} could not be read.",
                transactionTypeCode, transactionId);

            throw new SettlementRateUnavailableException();
        }
    }
}

/// <summary>
/// Accounting could not say whether the books are closed. Thrown rather than
/// answered null, because "no lock" and "could not ask" must not look the same to
/// a caller about to write to the general ledger.
/// </summary>
public sealed class PeriodLockUnavailableException : Exception
{
    public PeriodLockUnavailableException()
        : base("Whether the books are closed could not be established.")
    {
    }
}

/// <summary>
/// Accounting could not say what a settled document was booked at. Thrown for the
/// same reason the period lock is: an unreadable rate and a matching rate must not
/// look the same, or a lookup that blipped would post the whole payment at today's
/// rate and leave a residue on the contact that nobody would think to look for.
/// </summary>
public sealed class SettlementRateUnavailableException : Exception
{
    public SettlementRateUnavailableException()
        : base("What the settled document was booked at could not be established.")
    {
    }
}
