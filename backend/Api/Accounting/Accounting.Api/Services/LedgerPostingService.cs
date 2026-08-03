using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

/// <summary>
/// Writes general-ledger rows on behalf of the service that owns the document.
///
/// <b>This is the only code in the product that inserts into
/// <c>acc.JournalLedger</c>.</b> Other services describe a posting — which
/// accounts, which amounts, which document — and this decides what actually
/// lands. Two writers would mean two opinions about what an account means, and
/// the disagreement would only ever surface as a report that does not tie.
///
/// <b>A posting is replaced, not appended.</b> The key is the document, its line
/// and the leg type; posting the same key again deletes what was there and
/// writes the new set inside one transaction. That is what makes a caller free
/// to retry after a dropped response, and what lets a cost that has been
/// restated correct its own ledger rows rather than needing a second entry
/// nobody asked for.
///
/// <b>One call carries every leg type a document has</b>, because no subset of
/// an invoice's legs balances on its own: the revenue credits are per line, the
/// GST credits are per rate, and the single receivable debit is neither. Balance
/// is therefore checked across the whole request rather than per key.
///
/// <b>Two services write to one document, and neither disturbs the other.</b>
/// Sales posts an invoice's ITEM, TAX, CONTROL and ROUNDOFF legs; Inventory's
/// costing worker posts the COGS legs onto the same invoice minutes later. Each
/// replaces only the keys its own legs name, so the two are independent without
/// either knowing the other exists.
///
/// <b>One document can also be several things at once.</b> A payment that runs
/// past what was owed settles a bill with part of itself and leaves the rest as
/// an advance — so the ledger source is a property of the leg, not of the
/// posting, and a payables report reading bill payments still sees the part that
/// was one.
/// </summary>
public sealed class LedgerPostingService
{
    private readonly AccountingDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IBaseCurrencyProvider _baseCurrency;

    public LedgerPostingService(
        AccountingDbContext db, TenantContext tenant, IBaseCurrencyProvider baseCurrency)
    {
        _db = db;
        _tenant = tenant;
        _baseCurrency = baseCurrency;
    }

    public async Task<PostLedgerResult> PostAsync(PostLedgerRequest request, CancellationToken ct)
    {
        if (_tenant.CustomerId is null || _tenant.OrgId is null)
        {
            return new PostLedgerResult(PostLedgerOutcome.TenantMissing, 0, 0);
        }

        string currency;
        decimal rate;

        if (request.CurrencyCode is { Length: 3 } supplied)
        {
            currency = supplied.ToUpperInvariant();
            rate = request.ExchangeRate is decimal suppliedRate && suppliedRate > 0
                ? suppliedRate
                : 1m;
        }
        else
        {
            // The branch's own currency, not a default. A posting stamped with a
            // guessed currency is wrong in a total nobody re-reads.
            if (await _baseCurrency.GetBaseCurrencyAsync(ct) is not { } resolved)
            {
                return new PostLedgerResult(PostLedgerOutcome.BaseCurrencyUnavailable, 0, 0);
            }

            currency = resolved;
            rate = 1m;
        }

        if (request.Legs.Count == 0 && request.WithdrawLedgerTypeIds.Count == 0)
        {
            return new PostLedgerResult(
                PostLedgerOutcome.WithdrawalTypesMissing, 0, 0,
                "A withdrawal has no legs to infer its leg types from, so it has to name them.");
        }

        string typeCode = request.TransactionTypeCode.ToUpperInvariant();
        var rows = new List<JournalLedger>(request.Legs.Count);

        foreach (LedgerLegRequest leg in request.Legs)
        {
            // How the leg reads back in a refusal. The system name when there is
            // one, because that is what the caller wrote.
            string described = leg.AccountSystemName is { Length: > 0 } named
                ? $"'{named}'"
                : leg.AccountId is long identified ? $"account {identified}" : "an unnamed account";

            // Exactly one side. Checked here as well as by the database so the
            // caller gets a reason rather than a constraint violation.
            if ((leg.DebitAmount == 0) == (leg.CreditAmount == 0))
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.LegNotExclusive, 0, 0,
                    $"{described}: a leg is a debit or a credit, never both or neither.");
            }

            if (leg.DebitAmount < 0 || leg.CreditAmount < 0)
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.LegNotExclusive, 0, 0,
                    $"{described}: an amount is negative. A reversal is an "
                        + "offsetting entry, not a negative one.");
            }

            // Named either way: an id from Accounting's own documents, a system
            // name from every other service. Both resolve through the query
            // filter, so neither can reach another branch's chart.
            Account? account = leg.AccountId is long accountId
                ? await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct)
                : leg.AccountSystemName is { Length: > 0 } systemName
                    ? await _db.Accounts.FirstOrDefaultAsync(
                        a => a.AccountSystemName == systemName, ct)
                    : null;

            if (account is null)
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.AccountMissing, 0, 0,
                    leg.AccountId is null && leg.AccountSystemName is null or { Length: 0 }
                        ? "A leg has to name its account, by system name or by id."
                        : $"The chart of accounts has no {described}.");
            }

            if (account.IsLock || !account.IsActive)
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.AccountLocked, 0, 0,
                    $"'{account.AccountName}' is frozen for posting.");
            }

            long? subAccountId = null;

            if (leg.SubAccountId is long suppliedSubAccountId)
            {
                SubAccount? sub = await _db.SubAccounts.FirstOrDefaultAsync(
                    s => s.SubAccountId == suppliedSubAccountId, ct);

                // Under this leg's own account, or the balance it produces
                // reconciles against neither account's subledger.
                if (sub is null || sub.AccountId != account.AccountId)
                {
                    return new PostLedgerResult(
                        PostLedgerOutcome.SubAccountMissing, 0, 0,
                        $"Sub-account {suppliedSubAccountId} does not sit under "
                            + $"'{account.AccountName}'.");
                }

                subAccountId = sub.SubAccountId;
            }
            else if (leg.SubAccountReferenceType is { } referenceType
                && leg.SubAccountReferenceId is { } referenceId)
            {
                SubAccount? sub = await _db.SubAccounts.FirstOrDefaultAsync(
                    s => s.AccountId == account.AccountId
                        && s.ReferenceType == referenceType
                        && s.ReferenceId == referenceId,
                    ct);

                if (sub is null)
                {
                    return new PostLedgerResult(
                        PostLedgerOutcome.SubAccountMissing, 0, 0,
                        $"'{account.AccountName}' has no sub-account for {referenceType} "
                            + $"{referenceId}. The master that owns it has not been provisioned.");
                }

                subAccountId = sub.SubAccountId;
            }

            rows.Add(new JournalLedger
            {
                LedgerDate = request.LedgerDate,
                AccountId = account.AccountId,
                SubAccountId = subAccountId,
                TransactionTypeCode = typeCode,
                TransactionId = request.TransactionId,
                TransactionDetailId = leg.TransactionDetailId,
                DebitAmount = leg.DebitAmount,
                CreditAmount = leg.CreditAmount,
                DebitAmountBase = Base(leg.DebitAmount, rate),
                CreditAmountBase = Base(leg.CreditAmount, rate),
                CurrencyCode = currency,
                ExchangeRate = rate,
                ContactId = request.ContactId,
                LedgerTypeId = leg.LedgerTypeId,
                LedgerSourceId = leg.LedgerSourceId,
                SourceDocumentId = request.SourceDocumentId,
                TransactionDesc = leg.TransactionDesc,
                JournalId = request.JournalId,
            });

            // First use freezes the account's nature. Set in the same
            // transaction as the row that made it true, so nothing can
            // reclassify an account that already holds postings.
            account.IsUsed = true;
        }

        // Balance in base currency, which is what the trigger checks and what
        // every report sums. Rounding each leg independently can put a rate
        // conversion a paisa out, and the caller has to fix that rather than
        // the database silently absorbing it.
        decimal debits = rows.Sum(r => r.DebitAmountBase);
        decimal credits = rows.Sum(r => r.CreditAmountBase);

        if (rows.Count > 0 && debits != credits)
        {
            return new PostLedgerResult(
                PostLedgerOutcome.Unbalanced, 0, 0,
                $"Debits total {debits} and credits total {credits}.");
        }

        // Join the caller's transaction when there is one — a manual journal
        // allocates its number, flips its status and posts its legs, and those
        // three are one act. Only open a transaction when nothing else has.
        IDbContextTransaction? own = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            int replaced = await ReplaceAsync(request, typeCode, rows, ct);

            // ExecuteDelete goes straight to the database and the change tracker
            // never hears about it. A row this context had already read is now
            // tracked with nothing behind it, and the SaveChanges below would
            // try to write it again against a row count of zero.
            foreach (var stale in _db.ChangeTracker.Entries<JournalLedger>()
                .Where(e => e.Entity.TransactionTypeCode == typeCode
                    && e.Entity.TransactionId == request.TransactionId)
                .ToList())
            {
                stale.State = EntityState.Detached;
            }

            _db.JournalLedger.AddRange(rows);
            await _db.SaveChangesAsync(ct);

            // The balance trigger is deferred, so it fires at commit rather than
            // on the first row — which is the only way a multi-leg posting can
            // be inserted at all. When the caller owns the transaction, that
            // commit is theirs and so is the check.
            if (own is not null)
            {
                await own.CommitAsync(ct);
            }

            return new PostLedgerResult(PostLedgerOutcome.Ok, rows.Count, replaced);
        }
        finally
        {
            if (own is not null)
            {
                await own.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Clears what this posting is about to supersede, and nothing else.
    ///
    /// One delete per leg type, each narrowed to the document lines that type
    /// actually names. A single delete over the union of types and the union of
    /// lines would be the cross product of the two — it would take an ITEM row on
    /// a line this request never mentioned.
    ///
    /// <b>A withdrawal is document-wide by leg type</b>, because it has no lines
    /// to narrow by and its whole claim is that none of those legs exist any
    /// more. A posting cannot be document-wide: Inventory posts a document one
    /// movement at a time, so line two would erase line one.
    /// </summary>
    private async Task<int> ReplaceAsync(
        PostLedgerRequest request,
        string typeCode,
        List<JournalLedger> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            List<int> withdrawing = request.WithdrawLedgerTypeIds.Distinct().ToList();

            return await _db.JournalLedger
                .Where(l => l.TransactionTypeCode == typeCode
                    && l.TransactionId == request.TransactionId
                    && withdrawing.Contains(l.LedgerTypeId))
                .ExecuteDeleteAsync(ct);
        }

        int replaced = 0;

        foreach (IGrouping<int, JournalLedger> byType in rows.GroupBy(r => r.LedgerTypeId))
        {
            int ledgerTypeId = byType.Key;
            List<long> details = byType.Select(r => r.TransactionDetailId).Distinct().ToList();

            replaced += await _db.JournalLedger
                .Where(l => l.TransactionTypeCode == typeCode
                    && l.TransactionId == request.TransactionId
                    && l.LedgerTypeId == ledgerTypeId
                    && details.Contains(l.TransactionDetailId))
                .ExecuteDeleteAsync(ct);
        }

        return replaced;
    }

    /// <summary>
    /// The base-currency amount. Two decimals, away from zero, because a ledger
    /// row is money and banker's rounding on a half-paisa would drift a total
    /// that has to tie exactly.
    /// </summary>
    private static decimal Base(decimal amount, decimal rate) =>
        rate == 1m ? amount : Math.Round(amount / rate, 2, MidpointRounding.AwayFromZero);
}
