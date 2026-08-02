using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
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
/// nobody asked for. An empty leg list withdraws the posting.
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

        var rows = new List<JournalLedger>(request.Legs.Count);

        foreach (LedgerLegRequest leg in request.Legs)
        {
            // Exactly one side. Checked here as well as by the database so the
            // caller gets a reason rather than a constraint violation.
            if ((leg.DebitAmount == 0) == (leg.CreditAmount == 0))
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.LegNotExclusive, 0, 0,
                    $"{leg.AccountSystemName}: a leg is a debit or a credit, never both or neither.");
            }

            if (leg.DebitAmount < 0 || leg.CreditAmount < 0)
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.LegNotExclusive, 0, 0,
                    $"{leg.AccountSystemName}: an amount is negative. A reversal is an "
                        + "offsetting entry, not a negative one.");
            }

            Account? account = await _db.Accounts.FirstOrDefaultAsync(
                a => a.AccountSystemName == leg.AccountSystemName, ct);

            if (account is null)
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.AccountMissing, 0, 0,
                    $"The chart of accounts has no account named '{leg.AccountSystemName}'.");
            }

            if (account.IsLock || !account.IsActive)
            {
                return new PostLedgerResult(
                    PostLedgerOutcome.AccountLocked, 0, 0,
                    $"'{leg.AccountSystemName}' is frozen for posting.");
            }

            long? subAccountId = null;

            if (leg.SubAccountReferenceType is { } referenceType
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
                        $"'{leg.AccountSystemName}' has no sub-account for {referenceType} "
                            + $"{referenceId}. The master that owns it has not been provisioned.");
                }

                subAccountId = sub.SubAccountId;
            }

            rows.Add(new JournalLedger
            {
                LedgerDate = request.LedgerDate,
                AccountId = account.AccountId,
                SubAccountId = subAccountId,
                TransactionTypeCode = request.TransactionTypeCode.ToUpperInvariant(),
                TransactionId = request.TransactionId,
                TransactionDetailId = request.TransactionDetailId,
                DebitAmount = leg.DebitAmount,
                CreditAmount = leg.CreditAmount,
                DebitAmountBase = Base(leg.DebitAmount, rate),
                CreditAmountBase = Base(leg.CreditAmount, rate),
                CurrencyCode = currency,
                ExchangeRate = rate,
                ContactId = request.ContactId,
                LedgerTypeId = request.LedgerTypeId,
                LedgerSourceId = request.LedgerSourceId,
                SourceDocumentId = request.SourceDocumentId,
                TransactionDesc = leg.TransactionDesc,
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

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // The replace. Scoped to this caller's own key, so a document whose
        // other legs were written by another service keeps them.
        int replaced = await _db.JournalLedger
            .Where(l => l.TransactionTypeCode == request.TransactionTypeCode
                && l.TransactionId == request.TransactionId
                && l.TransactionDetailId == request.TransactionDetailId
                && l.LedgerTypeId == request.LedgerTypeId)
            .ExecuteDeleteAsync(ct);

        _db.JournalLedger.AddRange(rows);
        await _db.SaveChangesAsync(ct);

        // The balance trigger is deferred, so it fires here rather than on the
        // first row — which is the only way a multi-leg posting can be inserted
        // at all.
        await tx.CommitAsync(ct);

        return new PostLedgerResult(PostLedgerOutcome.Ok, rows.Count, replaced);
    }

    /// <summary>
    /// The base-currency amount. Two decimals, away from zero, because a ledger
    /// row is money and banker's rounding on a half-paisa would drift a total
    /// that has to tie exactly.
    /// </summary>
    private static decimal Base(decimal amount, decimal rate) =>
        rate == 1m ? amount : Math.Round(amount / rate, 2, MidpointRounding.AwayFromZero);
}
