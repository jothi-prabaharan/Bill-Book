using Banking.Entity.Enums;
using Banking.Entity.Models;
using Banking.Entity.TableEntities;
using Banking.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Banking.Api.Services;

/// <summary>
/// Money moving between the organization's own accounts — bank to bank, or bank
/// to the cash drawer.
///
/// <b>The simplest document that posts.</b> No contact, no allocation, no
/// sub-account: two legs, one debiting the destination's ledger account and one
/// crediting the source's, both under <c>MONEYTRANSFER</c>. It is also the only
/// money document that can be built and tested end to end today, because it
/// settles nothing and so waits on no invoice or bill.
///
/// A transfer moves no value in or out — the organization owns exactly what it
/// owned before — so the trial balance is unchanged by one. What changes is which
/// account holds it.
/// </summary>
public sealed class TransferMoneyService
{
    private const string TypeCode = "TRM";

    private readonly BankingDbContext _db;
    private readonly IAccountingLedger _ledger;
    private readonly INumberGenerator _numbers;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public TransferMoneyService(
        BankingDbContext db,
        IAccountingLedger ledger,
        INumberGenerator numbers,
        IBaseCurrencyProvider baseCurrency,
        ICurrentUser user,
        TimeProvider clock)
    {
        _db = db;
        _ledger = ledger;
        _numbers = numbers;
        _baseCurrency = baseCurrency;
        _user = user;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MoneyDocumentListItem>> ListAsync(
        string? status, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        IQueryable<TransferMoney> query = _db.TransferMoney;

        if (Enum.TryParse(status, ignoreCase: true, out MoneyDocumentStatus wanted))
        {
            query = query.Where(d => d.Status == wanted);
        }

        if (from is DateOnly start)
        {
            query = query.Where(d => d.TransactionDate >= start);
        }

        if (to is DateOnly end)
        {
            query = query.Where(d => d.TransactionDate <= end);
        }

        return await Summarize(query
            .OrderByDescending(d => d.TransactionDate)
            .ThenByDescending(d => d.TransferMoneyId))
            .ToListAsync(ct);
    }

    public async Task<MoneyDocumentListItem?> GetAsync(long id, CancellationToken ct) =>
        await Summarize(_db.TransferMoney.Where(d => d.TransferMoneyId == id))
            .FirstOrDefaultAsync(ct);

    public async Task<MoneyDocumentResult> CreateAsync(
        SaveTransferRequest request, CancellationToken ct)
    {
        if (request.FromBankAccountId == request.ToBankAccountId)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.SameAccount, 0,
                "Money cannot be transferred to the account it came from.");
        }

        if (await ResolveBankAsync(request.FromBankAccountId, ct) is null
            || await ResolveBankAsync(request.ToBankAccountId, ct) is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.BankAccountUnusable, 0,
                "One of those accounts is not in this branch, or has no ledger account behind it.");
        }

        (string? currency, decimal rate) = await ResolveCurrencyAsync(request, ct);
        if (currency is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.PostingRefused, 0,
                "The branch's base currency could not be read. Nothing was saved.");
        }

        var transfer = new TransferMoney
        {
            TransactionDate = request.TransactionDate,
            FromBankAccountId = request.FromBankAccountId,
            ToBankAccountId = request.ToBankAccountId,
            Amount = request.Amount,
            CurrencyCode = currency,
            ExchangeRate = rate,
            PaymentMethod = request.PaymentMethod,
            ReferenceNo = request.ReferenceNo,
            ReferenceDate = request.ReferenceDate,
            Memo = request.Memo,
            Status = MoneyDocumentStatus.Draft,
        };

        _db.TransferMoney.Add(transfer);
        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, transfer.TransferMoneyId);
    }

    public async Task<MoneyDocumentResult> UpdateAsync(
        long id, SaveTransferRequest request, CancellationToken ct)
    {
        TransferMoney? transfer =
            await _db.TransferMoney.FirstOrDefaultAsync(d => d.TransferMoneyId == id, ct);

        if (transfer is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (transfer.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        if (request.FromBankAccountId == request.ToBankAccountId)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.SameAccount, id,
                "Money cannot be transferred to the account it came from.");
        }

        (string? currency, decimal rate) = await ResolveCurrencyAsync(request, ct);
        if (currency is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.PostingRefused, id,
                "The branch's base currency could not be read. Nothing was saved.");
        }

        transfer.TransactionDate = request.TransactionDate;
        transfer.FromBankAccountId = request.FromBankAccountId;
        transfer.ToBankAccountId = request.ToBankAccountId;
        transfer.Amount = request.Amount;
        transfer.CurrencyCode = currency;
        transfer.ExchangeRate = rate;
        transfer.PaymentMethod = request.PaymentMethod;
        transfer.ReferenceNo = request.ReferenceNo;
        transfer.ReferenceDate = request.ReferenceDate;
        transfer.Memo = request.Memo;

        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    public async Task<MoneyDocumentResult> DeleteAsync(long id, CancellationToken ct)
    {
        TransferMoney? transfer =
            await _db.TransferMoney.FirstOrDefaultAsync(d => d.TransferMoneyId == id, ct);

        if (transfer is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (transfer.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        _db.TransferMoney.Remove(transfer);
        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary>
    /// Two legs and nothing else. The ledger call goes before the number and the
    /// status flip for the same reason it does on a payment: a posting written
    /// for a document that then fails to flip is replaced on the retry, while a
    /// document marked posted with no rows behind it is a silent hole.
    /// </summary>
    public async Task<MoneyDocumentResult> PostAsync(long id, CancellationToken ct)
    {
        TransferMoney? transfer =
            await _db.TransferMoney.FirstOrDefaultAsync(d => d.TransferMoneyId == id, ct);

        if (transfer is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (transfer.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        if (await MoneyPosting.CheckPeriodAsync(_ledger, transfer.TransactionDate, ct)
            is { } closed)
        {
            return closed with { DocumentId = id };
        }

        long? from = await ResolveBankAsync(transfer.FromBankAccountId, ct);
        long? to = await ResolveBankAsync(transfer.ToBankAccountId, ct);

        if (from is null || to is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.BankAccountUnusable, id,
                "One of those accounts has no ledger account behind it, so the transfer "
                    + "cannot be posted.");
        }

        LedgerPostOutcome posted = await _ledger.PostAsync(
            new LedgerPosting(
                TypeCode, id, transfer.TransactionDate, transfer.CurrencyCode,
                transfer.ExchangeRate, ContactId: null,
                [
                    // Into the destination, out of the source. Both under
                    // MONEYTRANSFER, which is the one ledger source with no
                    // counterparty behind it.
                    MoneyPosting.Bank(
                        1, MoneyPostingMap.MoneyTransferSource, to.Value,
                        debit: transfer.Amount, credit: 0m, transfer.Memo),

                    MoneyPosting.Bank(
                        2, MoneyPostingMap.MoneyTransferSource, from.Value,
                        debit: 0m, credit: transfer.Amount, transfer.Memo),
                ]),
            ct);

        if (posted != LedgerPostOutcome.Posted)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.PostingRefused, id,
                "The ledger did not accept the transfer, so nothing was posted.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        NumberAllocation allocation = await _numbers.NextAsync(
            TypeCode, transfer.TransactionDate, ct);

        transfer.TransactionNo = allocation.Code;
        transfer.Status = MoneyDocumentStatus.Posted;
        transfer.PostedAt = _clock.GetUtcNow();
        transfer.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    public async Task<MoneyDocumentResult> VoidAsync(
        long id, VoidMoneyDocumentRequest request, CancellationToken ct)
    {
        TransferMoney? transfer =
            await _db.TransferMoney.FirstOrDefaultAsync(d => d.TransferMoneyId == id, ct);

        if (transfer is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (transfer.Status != MoneyDocumentStatus.Posted)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotPosted, id);
        }

        if (await MoneyPosting.CheckPeriodAsync(_ledger, transfer.TransactionDate, ct)
            is { } closed)
        {
            return closed with { DocumentId = id };
        }

        LedgerPostOutcome withdrawn = await _ledger.PostAsync(
            new LedgerPosting(
                TypeCode, id, transfer.TransactionDate, transfer.CurrencyCode,
                transfer.ExchangeRate, ContactId: null, [],
                WithdrawLedgerTypeIds: [MoneyPostingMap.ControlLedgerType]),
            ct);

        if (withdrawn != LedgerPostOutcome.Posted)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.PostingRefused, id,
                "The ledger rows could not be withdrawn, so the transfer is unchanged.");
        }

        transfer.Status = MoneyDocumentStatus.Void;
        transfer.VoidedAt = _clock.GetUtcNow();
        transfer.VoidedBy = _user.UserId;
        transfer.VoidReason = request.Reason;

        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    private async Task<long?> ResolveBankAsync(long bankAccountId, CancellationToken ct) =>
        await _db.BankAccounts
            .Where(a => a.BankAccountId == bankAccountId && a.IsActive)
            .Select(a => a.LedgerAccountId)
            .FirstOrDefaultAsync(ct);

    private async Task<(string? Currency, decimal Rate)> ResolveCurrencyAsync(
        SaveTransferRequest request, CancellationToken ct)
    {
        if (request.CurrencyCode is { Length: 3 } supplied)
        {
            return (supplied.ToUpperInvariant(),
                request.ExchangeRate is decimal given && given > 0 ? given : 1m);
        }

        return (await _baseCurrency.GetBaseCurrencyAsync(ct), 1m);
    }

    private IQueryable<MoneyDocumentListItem> Summarize(IQueryable<TransferMoney> transfers) =>
        from t in transfers
        join source in _db.BankAccounts on t.FromBankAccountId equals source.BankAccountId
        join destination in _db.BankAccounts on t.ToBankAccountId equals destination.BankAccountId
        select new MoneyDocumentListItem
        {
            DocumentId = t.TransferMoneyId,
            TransactionNo = t.TransactionNo,
            TransactionDate = t.TransactionDate,
            BankAccountId = t.FromBankAccountId,
            BankAccountName = source.AccountName,
            ToBankAccountId = t.ToBankAccountId,
            ToBankAccountName = destination.AccountName,
            ContactId = null,
            Amount = t.Amount,
            CurrencyCode = t.CurrencyCode,
            ExchangeRate = t.ExchangeRate,
            PaymentMethod = t.PaymentMethod.ToString(),
            ReferenceNo = t.ReferenceNo,
            Memo = t.Memo,
            Status = t.Status.ToString(),
            PostedAt = t.PostedAt,
        };
}
