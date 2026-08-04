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
/// Money arriving — an invoice settled, an advance taken from a customer, a
/// supplier refunding what was overpaid.
///
/// <b>Draft, Posted, Void, and no way back.</b> A draft is free: its lines need
/// not add up, it holds no number and it touches no ledger. Posting takes the
/// number, writes the ledger rows and freezes the document. A posted receipt is
/// never edited — it is voided, which withdraws its ledger rows and leaves the
/// document and its number in place, because a gap in a document series is what
/// an auditor asks about.
///
/// <b>What kind of receipt it is lives on the lines.</b> Taking ₹11,000 against a
/// ₹10,000 invoice is an invoice payment and a customer overpayment at once; each
/// line carries its own ledger source, and <see cref="MoneyPostingMap"/> turns
/// that into the account the line credits.
///
/// The mirror of <see cref="SpendMoneyService"/> in every respect but the
/// direction of each pair of legs.
/// </summary>
public sealed class ReceiveMoneyService
{
    private const string TypeCode = "RCM";

    private readonly BankingDbContext _db;
    private readonly IAccountingLedger _ledger;
    private readonly INumberGenerator _numbers;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ReceiveMoneyService(
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
        IQueryable<ReceiveMoney> query = _db.ReceiveMoney;

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
            .ThenByDescending(d => d.ReceiveMoneyId))
            .ToListAsync(ct);
    }

    public async Task<MoneyDocumentView?> GetAsync(long id, CancellationToken ct)
    {
        MoneyDocumentListItem? header =
            await Summarize(_db.ReceiveMoney.Where(d => d.ReceiveMoneyId == id)).FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return null;
        }

        List<MoneyLineView> lines = await _db.ReceiveMoneyDetails
            .Where(l => l.ReceiveMoneyId == id)
            .OrderBy(l => l.LineNumber)
            .Select(l => new MoneyLineView
            {
                DetailId = l.ReceiveMoneyDetailId,
                LineNumber = l.LineNumber,
                LedgerSourceId = l.LedgerSourceId,
                MappingTransactionTypeCode = l.MappingTransactionTypeCode,
                MappingTransactionId = l.MappingTransactionId,
                Amount = l.Amount,
                AmountBase = l.AmountBase,
                LineMemo = l.LineMemo,
            })
            .ToListAsync(ct);

        return MoneyDocumentMapping.WithLines(header, lines);
    }

    public async Task<MoneyDocumentResult> CreateAsync(
        SaveMoneyDocumentRequest request, CancellationToken ct)
    {
        MoneyDocumentResult? invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        if (await ResolveBankAsync(request.BankAccountId, ct) is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.BankAccountUnusable, 0,
                "That account is not in this branch, or has no ledger account behind it yet.");
        }

        (string? currency, decimal rate) = await ResolveCurrencyAsync(request, ct);
        if (currency is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.PostingRefused, 0,
                "The branch's base currency could not be read. Nothing was saved.");
        }

        var document = new ReceiveMoney
        {
            TransactionDate = request.TransactionDate,
            BankAccountId = request.BankAccountId,
            ContactId = request.ContactId,
            Amount = request.Amount,
            CurrencyCode = currency,
            ExchangeRate = rate,
            PaymentMethod = request.PaymentMethod,
            ReferenceNo = request.ReferenceNo,
            ReferenceDate = request.ReferenceDate,
            Memo = request.Memo,
            MappingTransactionTypeCode = request.MappingTransactionTypeCode,
            MappingTransactionId = request.MappingTransactionId,
            Status = MoneyDocumentStatus.Draft,
        };

        _db.ReceiveMoney.Add(document);
        await _db.SaveChangesAsync(ct);

        _db.ReceiveMoneyDetails.AddRange(BuildLines(document, request.Lines));
        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, document.ReceiveMoneyId);
    }

    /// <summary>
    /// Replaces a draft wholesale. Lines are rewritten rather than matched up: an
    /// allocation is edited by moving amounts between lines as often as by
    /// changing one, and reconciling that against line ids would be guesswork.
    /// </summary>
    public async Task<MoneyDocumentResult> UpdateAsync(
        long id, SaveMoneyDocumentRequest request, CancellationToken ct)
    {
        ReceiveMoney? document = await _db.ReceiveMoney.FirstOrDefaultAsync(d => d.ReceiveMoneyId == id, ct);

        if (document is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (document.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        MoneyDocumentResult? invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        (string? currency, decimal rate) = await ResolveCurrencyAsync(request, ct);
        if (currency is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.PostingRefused, id,
                "The branch's base currency could not be read. Nothing was saved.");
        }

        document.TransactionDate = request.TransactionDate;
        document.BankAccountId = request.BankAccountId;
        document.ContactId = request.ContactId;
        document.Amount = request.Amount;
        document.CurrencyCode = currency;
        document.ExchangeRate = rate;
        document.PaymentMethod = request.PaymentMethod;
        document.ReferenceNo = request.ReferenceNo;
        document.ReferenceDate = request.ReferenceDate;
        document.Memo = request.Memo;
        document.MappingTransactionTypeCode = request.MappingTransactionTypeCode;
        document.MappingTransactionId = request.MappingTransactionId;

        await _db.ReceiveMoneyDetails.Where(l => l.ReceiveMoneyId == id).ExecuteDeleteAsync(ct);

        // ExecuteDelete goes straight to the database and the change tracker
        // never hears about it, so any line this context had already read is now
        // tracked with nothing behind it.
        foreach (var stale in _db.ChangeTracker.Entries<ReceiveMoneyDetail>()
            .Where(e => e.Entity.ReceiveMoneyId == id).ToList())
        {
            stale.State = EntityState.Detached;
        }

        _db.ReceiveMoneyDetails.AddRange(BuildLines(document, request.Lines));
        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    public async Task<MoneyDocumentResult> DeleteAsync(long id, CancellationToken ct)
    {
        ReceiveMoney? document = await _db.ReceiveMoney.FirstOrDefaultAsync(d => d.ReceiveMoneyId == id, ct);

        if (document is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (document.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        _db.ReceiveMoney.Remove(document);
        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary>
    /// Posts a draft: takes its number, writes its ledger rows and freezes it.
    ///
    /// The number allocation and the status flip are one transaction, so a post
    /// that fails gives the number back rather than leaving a hole in a series
    /// that has to be gapless. The ledger call is an HTTP hop and cannot join
    /// that transaction, so it goes <b>first</b> — a posting written for a
    /// document that then fails to flip is replaced on the retry, whereas a
    /// document marked posted with no ledger rows behind it is a silent hole in
    /// the accounts.
    /// </summary>
    public async Task<MoneyDocumentResult> PostAsync(long id, CancellationToken ct)
    {
        ReceiveMoney? document = await _db.ReceiveMoney.FirstOrDefaultAsync(d => d.ReceiveMoneyId == id, ct);

        if (document is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (document.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        List<ReceiveMoneyDetail> lines = await _db.ReceiveMoneyDetails
            .Where(l => l.ReceiveMoneyId == id)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        MoneyDocumentResult? refused = await MoneyPosting.CheckPostableAsync(
            _ledger, document.TransactionDate, document.Amount,
            lines.Sum(l => l.Amount), lines.Count, ct);

        if (refused is not null)
        {
            return refused with { DocumentId = id };
        }

        long? bankAccountId = await ResolveBankAsync(document.BankAccountId, ct);
        if (bankAccountId is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.BankAccountUnusable, id,
                "That account has no ledger account behind it, so the receipt cannot be posted.");
        }

        var legs = new List<LedgerPostingLeg>(lines.Count * 2);

        foreach (ReceiveMoneyDetail line in lines)
        {
            if (MoneyPostingMap.ForReceive(line.LedgerSourceId) is not { } target)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.UnknownLedgerSource, id,
                    $"Line {line.LineNumber} is for something money cannot arrive under.");
            }

            // The control leg, and the bank leg that pays for it. One pair per
            // line, so each pair balances on its own and every row carries the
            // source that produced it — an overpayment's two halves stay
            // distinguishable in the ledger.
            legs.Add(MoneyPosting.Control(
                line.LineNumber, line.LedgerSourceId, target, document.ContactId,
                debit: 0m, credit: line.Amount, line.LineMemo));

            legs.Add(MoneyPosting.Bank(
                line.LineNumber, line.LedgerSourceId, bankAccountId.Value,
                debit: line.Amount, credit: 0m, line.LineMemo));
        }

        LedgerPostOutcome posted = await _ledger.PostAsync(
            new LedgerPosting(
                TypeCode, id, document.TransactionDate, document.CurrencyCode,
                document.ExchangeRate, document.ContactId, legs),
            ct);

        if (posted != LedgerPostOutcome.Posted)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.PostingRefused, id,
                "The ledger did not accept the receipt, so nothing was posted.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        NumberAllocation allocation = await _numbers.NextAsync(
            TypeCode, document.TransactionDate, ct);

        document.TransactionNo = allocation.Code;
        document.Status = MoneyDocumentStatus.Posted;
        document.PostedAt = _clock.GetUtcNow();
        document.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary>
    /// Withdraws a posted receipt: the ledger rows go, the document and its
    /// number stay. A void is refused into a closed period for the same reason a
    /// post is — it changes what the ledger says about that date.
    /// </summary>
    public async Task<MoneyDocumentResult> VoidAsync(
        long id, VoidMoneyDocumentRequest request, CancellationToken ct)
    {
        ReceiveMoney? document = await _db.ReceiveMoney.FirstOrDefaultAsync(d => d.ReceiveMoneyId == id, ct);

        if (document is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (document.Status != MoneyDocumentStatus.Posted)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotPosted, id);
        }

        MoneyDocumentResult? closed =
            await MoneyPosting.CheckPeriodAsync(_ledger, document.TransactionDate, ct);

        if (closed is not null)
        {
            return closed with { DocumentId = id };
        }

        // An empty leg list with the leg types named is how a posting is
        // withdrawn — see the ledger door.
        LedgerPostOutcome withdrawn = await _ledger.PostAsync(
            new LedgerPosting(
                TypeCode, id, document.TransactionDate, document.CurrencyCode,
                document.ExchangeRate, document.ContactId, [],
                WithdrawLedgerTypeIds: [MoneyPostingMap.ControlLedgerType]),
            ct);

        if (withdrawn != LedgerPostOutcome.Posted)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.PostingRefused, id,
                "The ledger rows could not be withdrawn, so the receipt is unchanged.");
        }

        document.Status = MoneyDocumentStatus.Void;
        document.VoidedAt = _clock.GetUtcNow();
        document.VoidedBy = _user.UserId;
        document.VoidReason = request.Reason;

        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary>
    /// The bank account's own GL account. Null when the account is not in this
    /// branch, or when provisioning left it unlinked — a receipt into an
    /// account with no ledger identity has nowhere to debit.
    /// </summary>
    private async Task<long?> ResolveBankAsync(long bankAccountId, CancellationToken ct) =>
        await _db.BankAccounts
            .Where(a => a.BankAccountId == bankAccountId && a.IsActive)
            .Select(a => a.LedgerAccountId)
            .FirstOrDefaultAsync(ct);

    private static MoneyDocumentResult? Validate(SaveMoneyDocumentRequest request)
    {
        foreach (SaveMoneyLineRequest line in request.Lines)
        {
            if (MoneyPostingMap.ForReceive(line.LedgerSourceId) is null)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.UnknownLedgerSource, 0,
                    "One of the lines is for something money cannot arrive under.");
            }
        }

        return null;
    }

    private async Task<(string? Currency, decimal Rate)> ResolveCurrencyAsync(
        SaveMoneyDocumentRequest request, CancellationToken ct)
    {
        if (request.CurrencyCode is { Length: 3 } supplied)
        {
            return (supplied.ToUpperInvariant(),
                request.ExchangeRate is decimal given && given > 0 ? given : 1m);
        }

        return (await _baseCurrency.GetBaseCurrencyAsync(ct), 1m);
    }

    private static IEnumerable<ReceiveMoneyDetail> BuildLines(
        ReceiveMoney document, List<SaveMoneyLineRequest> lines) =>
        lines.Select((line, index) => new ReceiveMoneyDetail
        {
            ReceiveMoneyId = document.ReceiveMoneyId,
            LineNumber = index + 1,
            LedgerSourceId = line.LedgerSourceId,
            MappingTransactionTypeCode = line.MappingTransactionTypeCode,
            MappingTransactionId = line.MappingTransactionId,
            Amount = line.Amount,
            AmountBase = MoneyPosting.Base(line.Amount, document.ExchangeRate),
            LineMemo = line.LineMemo,
        });

    private IQueryable<MoneyDocumentListItem> Summarize(IQueryable<ReceiveMoney> documents) =>
        from d in documents
        join a in _db.BankAccounts on d.BankAccountId equals a.BankAccountId
        select new MoneyDocumentListItem
        {
            DocumentId = d.ReceiveMoneyId,
            TransactionNo = d.TransactionNo,
            TransactionDate = d.TransactionDate,
            BankAccountId = d.BankAccountId,
            BankAccountName = a.AccountName,
            ContactId = d.ContactId,
            Amount = d.Amount,
            CurrencyCode = d.CurrencyCode,
            ExchangeRate = d.ExchangeRate,
            PaymentMethod = d.PaymentMethod.ToString(),
            ReferenceNo = d.ReferenceNo,
            Memo = d.Memo,
            Status = d.Status.ToString(),
            PostedAt = d.PostedAt,
            MappingTransactionTypeCode = d.MappingTransactionTypeCode,
            MappingTransactionId = d.MappingTransactionId,
        };
}
