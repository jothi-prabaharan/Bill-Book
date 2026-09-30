using Shared.Kernel.Audit;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Persistence;

namespace Accounting.Repository.Repositories;

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
/// line carries its own ledger source, and <see cref="MoneyPostingMapRepository"/> turns
/// that into the account the line credits.
///
/// The mirror of <see cref="SpendMoneyRepository"/> in every respect but the
/// direction of each pair of legs.
/// </summary>
public sealed class ReceiveMoneyRepository
{
    private const string TypeCode = "RCM";

    private readonly AccountingDbContext _db;
    private readonly IAccountingLedger _ledger;
    private readonly INumberGenerator _numbers;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly TdsRepository? _tds;

    private static readonly string TdsReceivableAccount = SystemAccountNames.Of(SystemAccount.TdsReceivable);

    public ReceiveMoneyRepository(
        AccountingDbContext db,
        IAccountingLedger ledger,
        INumberGenerator numbers,
        IBaseCurrencyProvider baseCurrency,
        ICurrentUser user,
        TimeProvider clock,
        TdsRepository? tds = null)
    {
        _db = db;
        _ledger = ledger;
        _numbers = numbers;
        _baseCurrency = baseCurrency;
        _user = user;
        _clock = clock;
        _tds = tds;
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
                ProjectId = l.ProjectId,
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

        if (await ProjectRulesRepository.RefusalAsync(_db, request.Lines.Select(l => l.ProjectId), ct) is string projectRefusal)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.ProjectRefused, Detail: projectRefusal);
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

        // Derived from the lines rather than taken from the caller: the header's
        // mapping is a summary of them, and a header free to disagree with its
        // own lines is a document that says one bill while paying three.
        (string? TypeCode, long? TransactionId) header =
            MoneyAllocationRepository.HeaderMapping(request.Lines);

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
            MappingTransactionTypeCode = header.TypeCode,
            MappingTransactionId = header.TransactionId,
            Status = MoneyDocumentStatus.Draft,
        };
        ApplyTds(document, request);

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

        if (await ProjectRulesRepository.RefusalAsync(_db, request.Lines.Select(l => l.ProjectId), ct) is string projectRefusal)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.ProjectRefused, Detail: projectRefusal);
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
        (string? headerType, long? headerId) = MoneyAllocationRepository.HeaderMapping(request.Lines);
        document.MappingTransactionTypeCode = headerType;
        document.MappingTransactionId = headerId;
        ApplyTds(document, request);

        await _db.ReceiveMoneyDetails.Where(l => l.ReceiveMoneyId == id).ExecuteDeleteLoggedAsync(_db, ct);

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

        // TDS the customer withheld (TK-117): the lines settle the gross, the
        // bank receives the net, and the difference is TDS Receivable.
        decimal withheld = document.TdsAmount;
        if (withheld > 0 && document.ExchangeRate != 1m)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, id,
                "TDS withheld by a customer is recorded in the branch's own currency. Enter this receipt in it, or leave the TDS out.");
        }

        MoneyDocumentResult? refused = await MoneyPostingRepository.CheckPostableAsync(
            _ledger, document.TransactionDate, document.Amount + withheld,
            lines.Sum(l => l.Amount), lines.Count, ct);

        if (refused is not null)
        {
            if (withheld > 0 && refused.Outcome == MoneyDocumentOutcome.NotAllocated)
            {
                return new MoneyDocumentResult(MoneyDocumentOutcome.NotAllocated, id,
                    $"The lines come to {lines.Sum(l => l.Amount):0.00}, but the amount received plus the TDS withheld is "
                        + $"{document.Amount + withheld:0.00}. The lines carry what the customer settled before TDS.");
            }

            return refused with { DocumentId = id };
        }

        IReadOnlyDictionary<int, decimal> bankShares = TdsWithholdingRepository.BankShares(
            lines.Select(l => (l.LineNumber, l.Amount, true)), withheld);

        long? bankAccountId = await ResolveBankAsync(document.BankAccountId, ct);
        if (bankAccountId is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.BankAccountUnusable, id,
                "That account has no ledger account behind it, so the receipt cannot be posted.");
        }

        // Which lines settle a balance booked at a different rate. Resolved before
        // any leg is built, because a rate that cannot be read has to stop the
        // whole document rather than leave one line posted at the wrong one.
        SettlementRatesResult settlement = await MoneySettlementRepository.ResolveAsync(
            _ledger, document.CurrencyCode, document.ExchangeRate,
            lines.Select(l => (l.LineNumber, l.MappingTransactionTypeCode, l.MappingTransactionId)),
            ct);

        if (settlement.Refusal is { } blocked)
        {
            return blocked with { DocumentId = id };
        }

        string? baseCurrency = await _baseCurrency.GetBaseCurrencyAsync(ct);
        if (baseCurrency is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.PostingRefused, id,
                "The branch's base currency could not be read, so nothing was posted.");
        }

        var legs = new List<LedgerPostingLeg>(lines.Count * 2);

        foreach (ReceiveMoneyDetail line in lines)
        {
            if (MoneyPostingMapRepository.ForReceive(line.LedgerSourceId) is not { } target)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.UnknownLedgerSource, id,
                    $"Line {line.LineNumber} is for something money cannot arrive under.");
            }

            decimal? settledRate = settlement.For(line.LineNumber);

            // The control leg, and the bank leg that pays for it. One pair per
            // line, so each pair balances on its own and every row carries the
            // source that produced it — an overpayment's two halves stay
            // distinguishable in the ledger.
            // The line's project rides on its control leg (TK-104); the bank
            // leg is the branch's cash, which belongs to no job.
            legs.Add(MoneyPostingRepository.Control(
                line.LineNumber, line.LedgerSourceId, target, document.ContactId,
                debit: 0m, credit: line.Amount, line.LineMemo, settledRate) with { ProjectId = line.ProjectId });

            // Less reaches the bank than the line settles when the customer withheld TDS.
            decimal received = bankShares[line.LineNumber];
            if (received > 0)
            {
                legs.Add(MoneyPostingRepository.Bank(
                    line.LineNumber, line.LedgerSourceId, bankAccountId.Value,
                    debit: received, credit: 0m, line.LineMemo));
            }

            // The pair no longer balances once the two ends convert at different
            // rates, and what it is out by is the realized gain or loss. The
            // arguments are the mirror of the spend side, and the leg it produces
            // takes whichever side is short — so the sign is never asserted twice.
            if (settledRate is decimal booked)
            {
                LedgerPostingLeg? fx = MoneyPostingRepository.Fx(
                    line.LineNumber,
                    line.LedgerSourceId,
                    baseCurrency,
                    controlDebitBase: 0m,
                    controlCreditBase: MoneyPostingRepository.Base(line.Amount, booked),
                    bankDebitBase: MoneyPostingRepository.Base(received, document.ExchangeRate),
                    bankCreditBase: 0m,
                    line.LineMemo);

                if (fx is not null)
                {
                    legs.Add(fx);
                }
            }
        }

        // What the customer withheld is claimed from the government, not the bank (TK-117).
        if (withheld > 0)
        {
            legs.Add(new LedgerPostingLeg(
                MoneyPostingMapRepository.ControlLedgerType, lines[0].LedgerSourceId, 0,
                TdsReceivableAccount, AccountId: null, SubAccountReferenceType: null, SubAccountReferenceId: null,
                MoneyPostingMapRepository.Primary, DebitAmount: withheld, CreditAmount: 0m,
                document.DeductorTan is { Length: > 0 } tan ? $"TDS withheld by {tan}" : "TDS withheld by the customer"));
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

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        NumberAllocation allocation = await _numbers.NextAsync(
            TypeCode, document.TransactionDate, ct);

        document.TransactionNo = allocation.Code;
        document.Status = MoneyDocumentStatus.Posted;
        document.PostedAt = _clock.GetUtcNow();
        document.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);

        // The register row the Form 26AS match reads (TK-117).
        if (_tds is not null && withheld > 0)
        {
            await _tds.WriteSufferedAsync(id, allocation.Code, document.TransactionDate, document.ContactId,
                document.TdsSectionCode, document.DeductorTan, document.Amount, withheld, ct);
        }

        await tx.CommitAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary>Copies the withheld TDS onto a draft (TK-117).</summary>
    private static void ApplyTds(ReceiveMoney document, SaveMoneyDocumentRequest request)
    {
        document.TdsAmount = request.TdsAmount;
        document.TdsSectionCode = request.TdsAmount > 0 && !string.IsNullOrWhiteSpace(request.TdsSectionCode)
            ? request.TdsSectionCode.Trim()
            : null;
        document.DeductorTan = request.TdsAmount > 0 && !string.IsNullOrWhiteSpace(request.DeductorTan)
            ? request.DeductorTan.Trim().ToUpperInvariant()
            : null;
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
            await MoneyPostingRepository.CheckPeriodAsync(_ledger, document.TransactionDate, ct);

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
                WithdrawLedgerTypeIds:
                    [MoneyPostingMapRepository.ControlLedgerType, MoneyPostingRepository.FxLedgerType]),
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

        if (_tds is not null)
        {
            await _tds.RemoveAsync(TypeCode, id, ct);
        }

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
        // A TAN that is not one could never be found in Form 26AS (TK-117).
        if (request.TdsAmount > 0 && !string.IsNullOrWhiteSpace(request.DeductorTan)
            && !TdsCalculationRepository.IsValidTan(request.DeductorTan))
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, 0,
                "A TAN is four letters, five digits and a letter, such as MUMA12345B.");
        }

        foreach (SaveMoneyLineRequest line in request.Lines)
        {
            if (MoneyPostingMapRepository.ForReceive(line.LedgerSourceId) is null)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.UnknownLedgerSource, 0,
                    "One of the lines is for something money cannot arrive under.");
            }
        }

        // What the lines say about the documents they settle: the right kind of
        // document for what each line is for, and each document named once.
        return MoneyAllocationRepository.Check(request.Lines, spending: false);
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
            AmountBase = MoneyPostingRepository.Base(line.Amount, document.ExchangeRate),
            LineMemo = line.LineMemo,
            ProjectId = line.ProjectId,
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
            TdsAmount = d.TdsAmount,
            TdsSectionCode = d.TdsSectionCode,
            DeductorTan = d.DeductorTan,
        };
}
