using Shared.Kernel.Audit;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Approvals;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Persistence;

namespace Accounting.Repository.Repositories;

/// <summary>
/// Money leaving the organization — a bill paid, a deposit put down with a
/// supplier, a customer refunded.
///
/// <b>Draft, Posted, Void, and no way back.</b> A draft is free: its lines need
/// not add up, it holds no number and it touches no ledger. Posting takes the
/// number, writes the ledger rows and freezes the document. A posted payment is
/// never edited — it is voided, which withdraws its ledger rows and leaves the
/// document and its number in place, because a gap in a document series is what
/// an auditor asks about.
///
/// <b>What kind of payment it is lives on the lines.</b> Paying ₹11,000 against a
/// ₹10,000 bill is a bill payment and a supplier deposit at once; each line
/// carries its own ledger source, and <see cref="MoneyPostingMapRepository"/> turns that
/// into the account the line debits.
/// </summary>
public sealed class SpendMoneyRepository
{
    private const string TypeCode = "SPM";

    private readonly AccountingDbContext _db;
    private readonly IAccountingLedger _ledger;
    private readonly INumberGenerator _numbers;
    private readonly IBaseCurrencyProvider _baseCurrency;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly AccountingApprovalRepository? _approvals;
    private readonly TdsRepository? _tds;
    private readonly ITenantContext? _tenant;

    /// <summary><c>mst.LedgerSources</c> 8 — a deposit placed with a supplier before their bill, which bears TDS (TK-117).</summary>
    private const int SupplierAdvanceSource = 8;

    private static readonly string TdsPayableAccount = SystemAccountNames.Of(SystemAccount.TdsPayable);

    public SpendMoneyRepository(
        AccountingDbContext db,
        IAccountingLedger ledger,
        INumberGenerator numbers,
        IBaseCurrencyProvider baseCurrency,
        ICurrentUser user,
        TimeProvider clock,
        AccountingApprovalRepository? approvals = null,
        TdsRepository? tds = null,
        ITenantContext? tenant = null)
    {
        _db = db;
        _ledger = ledger;
        _numbers = numbers;
        _baseCurrency = baseCurrency;
        _user = user;
        _clock = clock;
        _approvals = approvals;
        _tds = tds;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<MoneyDocumentListItem>> ListAsync(
        string? status, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        IQueryable<SpendMoney> query = _db.SpendMoney;

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
            .ThenByDescending(d => d.SpendMoneyId))
            .ToListAsync(ct);
    }

    public async Task<MoneyDocumentView?> GetAsync(long id, CancellationToken ct)
    {
        MoneyDocumentListItem? header =
            await Summarize(_db.SpendMoney.Where(d => d.SpendMoneyId == id)).FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return null;
        }

        List<MoneyLineView> lines = await _db.SpendMoneyDetails
            .Where(l => l.SpendMoneyId == id)
            .OrderBy(l => l.LineNumber)
            .Select(l => new MoneyLineView
            {
                DetailId = l.SpendMoneyDetailId,
                LineNumber = l.LineNumber,
                LedgerSourceId = l.LedgerSourceId,
                MappingTransactionTypeCode = l.MappingTransactionTypeCode,
                MappingTransactionId = l.MappingTransactionId,
                Amount = l.Amount,
                AmountBase = l.AmountBase,
                LineMemo = l.LineMemo,
                ProjectId = l.ProjectId,
                IsBillable = l.IsBillable,
                MarkupPercent = l.MarkupPercent,
                BilledInvoiceId = l.BilledInvoiceId,
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

        var document = new SpendMoney
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
        ApplyChallan(document, request);

        _db.SpendMoney.Add(document);
        await _db.SaveChangesAsync(ct);

        _db.SpendMoneyDetails.AddRange(BuildLines(document, request.Lines));
        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, document.SpendMoneyId);
    }

    /// <summary>
    /// Replaces a draft wholesale. Lines are rewritten rather than matched up: an
    /// allocation is edited by moving amounts between lines as often as by
    /// changing one, and reconciling that against line ids would be guesswork.
    /// </summary>
    public async Task<MoneyDocumentResult> UpdateAsync(
        long id, SaveMoneyDocumentRequest request, CancellationToken ct)
    {
        SpendMoney? document = await _db.SpendMoney.FirstOrDefaultAsync(d => d.SpendMoneyId == id, ct);

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
        ApplyChallan(document, request);

        await _db.SpendMoneyDetails.Where(l => l.SpendMoneyId == id).ExecuteDeleteLoggedAsync(_db, ct);

        // ExecuteDelete goes straight to the database and the change tracker
        // never hears about it, so any line this context had already read is now
        // tracked with nothing behind it.
        foreach (var stale in _db.ChangeTracker.Entries<SpendMoneyDetail>()
            .Where(e => e.Entity.SpendMoneyId == id).ToList())
        {
            stale.State = EntityState.Detached;
        }

        // An edit to a payment in approval, or approved, sends it back: an
        // approval approves what the approver saw (TK-101).
        bool returned = _approvals is not null
            && await _approvals.ReturnToDraftAsync(ApprovalRequestKind.SpendMoney, id, document, ct);

        _db.SpendMoneyDetails.AddRange(BuildLines(document, request.Lines));
        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id,
            returned ? "Saved. The payment was taken out of approval and must be submitted again." : null);
    }

    public async Task<MoneyDocumentResult> DeleteAsync(long id, CancellationToken ct)
    {
        SpendMoney? document = await _db.SpendMoney.FirstOrDefaultAsync(d => d.SpendMoneyId == id, ct);

        if (document is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (document.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        _db.SpendMoney.Remove(document);
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
        SpendMoney? document = await _db.SpendMoney.FirstOrDefaultAsync(d => d.SpendMoneyId == id, ct);

        if (document is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (document.Status != MoneyDocumentStatus.Draft)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotDraft);
        }

        if (_approvals is not null
            && await _approvals.GateAsync(ApprovalRequestKind.SpendMoney, document, ct) is string awaiting)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.AwaitingApproval, id, awaiting);
        }

        // A TDS challan pays the government what was deducted (TK-117). It has
        // no lines and posts on its own path.
        if (document.IsTdsChallan)
        {
            return await PostChallanAsync(document, ct);
        }

        List<SpendMoneyDetail> lines = await _db.SpendMoneyDetails
            .Where(l => l.SpendMoneyId == id)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        // TDS on a deposit placed with a supplier (TK-117): due on whichever
        // comes first, the payment or the bill. The lines keep the gross and
        // the amount paid is the gross less the deduction.
        (TdsQuote? tdsQuote, MoneyDocumentResult? tdsRefusal) = await QuoteAdvanceAsync(document, lines, ct);
        if (tdsRefusal is not null)
        {
            return tdsRefusal;
        }

        decimal withheld = tdsQuote is { Applies: true } quoted ? quoted.TdsAmount : 0m;

        MoneyDocumentResult? refused = await MoneyPostingRepository.CheckPostableAsync(
            _ledger, document.TransactionDate, document.Amount + withheld,
            lines.Sum(l => l.Amount), lines.Count, ct);

        if (refused is not null)
        {
            if (withheld > 0 && refused.Outcome == MoneyDocumentOutcome.NotAllocated)
            {
                decimal gross = lines.Sum(l => l.Amount);
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.TdsRefused, id,
                    $"TDS of {withheld:0.00} is due on this advance. {tdsQuote!.Note} Keep the lines at the gross "
                        + $"{gross:0.00} and enter {gross - withheld:0.00} as the amount paid.");
            }

            return refused with { DocumentId = id };
        }

        IReadOnlyDictionary<int, decimal> bankShares = TdsWithholdingRepository.BankShares(
            lines.Select(l => (l.LineNumber, l.Amount, l.LedgerSourceId == SupplierAdvanceSource)), withheld);

        long? bankAccountId = await ResolveBankAsync(document.BankAccountId, ct);
        if (bankAccountId is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.BankAccountUnusable, id,
                "That account has no ledger account behind it, so the payment cannot be posted.");
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

        foreach (SpendMoneyDetail line in lines)
        {
            if (MoneyPostingMapRepository.ForSpend(line.LedgerSourceId) is not { } target)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.UnknownLedgerSource, id,
                    $"Line {line.LineNumber} is for something money cannot leave under.");
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
                debit: line.Amount, credit: 0m, line.LineMemo, settledRate) with { ProjectId = line.ProjectId });

            // Less leaves the bank than the line carries when TDS was withheld from it.
            decimal paid = bankShares[line.LineNumber];
            if (paid > 0)
            {
                legs.Add(MoneyPostingRepository.Bank(
                    line.LineNumber, line.LedgerSourceId, bankAccountId.Value,
                    debit: 0m, credit: paid, line.LineMemo));
            }

            // The pair no longer balances once the two ends convert at different
            // rates, and what it is out by is the realized gain or loss.
            if (settledRate is decimal booked)
            {
                LedgerPostingLeg? fx = MoneyPostingRepository.Fx(
                    line.LineNumber,
                    line.LedgerSourceId,
                    baseCurrency,
                    controlDebitBase: MoneyPostingRepository.Base(line.Amount, booked),
                    controlCreditBase: 0m,
                    bankDebitBase: 0m,
                    bankCreditBase: MoneyPostingRepository.Base(paid, document.ExchangeRate),
                    line.LineMemo);

                if (fx is not null)
                {
                    legs.Add(fx);
                }
            }
        }

        // What was withheld is owed to the government, not the bank's (TK-117).
        if (withheld > 0)
        {
            legs.Add(new LedgerPostingLeg(
                MoneyPostingMapRepository.ControlLedgerType, SupplierAdvanceSource, 0,
                TdsPayableAccount, AccountId: null, SubAccountReferenceType: null, SubAccountReferenceId: null,
                MoneyPostingMapRepository.Primary, DebitAmount: 0m, CreditAmount: withheld,
                $"TDS under {tdsQuote!.SectionCode}"));
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
                "The ledger did not accept the payment, so nothing was posted.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        NumberAllocation allocation = await _numbers.NextAsync(
            TypeCode, document.TransactionDate, ct);

        document.TransactionNo = allocation.Code;
        document.Status = MoneyDocumentStatus.Posted;
        document.PostedAt = _clock.GetUtcNow();
        document.PostedBy = _user.UserId;
        document.TdsAmount = withheld;
        document.TdsSectionCode = tdsQuote is { Applies: true } ? tdsQuote.SectionCode : null;

        await _db.SaveChangesAsync(ct);

        // The register row, which counts toward the vendor's threshold even
        // when nothing was deducted (TK-117).
        if (_tds is not null && tdsQuote is { Applies: true })
        {
            await _tds.WriteDeductionAsync(TypeCode, id, allocation.Code, document.TransactionDate, document.ContactId, tdsQuote, ct);
        }

        await tx.CommitAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary>
    /// What TDS a supplier deposit owes (TK-117). Nothing when there is no
    /// deposit line, when TDS is not wired, or on a foreign-currency payment —
    /// a non-resident is paid under section 195, which is not built.
    /// </summary>
    private async Task<(TdsQuote? Quote, MoneyDocumentResult? Refusal)> QuoteAdvanceAsync(
        SpendMoney document, List<SpendMoneyDetail> lines, CancellationToken ct)
    {
        decimal advance = lines.Where(l => l.LedgerSourceId == SupplierAdvanceSource).Sum(l => l.Amount);
        if (_tds is null || advance == 0m || document.ExchangeRate != 1m)
        {
            return (null, null);
        }

        TdsQuoteResult result = await _tds.QuoteAsync(new TdsQuoteRequest
        {
            CustomerId = _tenant?.CustomerId ?? Guid.Empty,
            OrgId = _tenant?.OrgId ?? Guid.Empty,
            ContactId = document.ContactId,
            TransactionTypeCode = TypeCode,
            TransactionId = document.SpendMoneyId,
            Date = document.TransactionDate,
            Amount = advance,
            Kind = (int)TdsEntryKind.Advance,
        }, ct);

        return result.Outcome switch
        {
            TdsQuoteOutcome.Ok => (result.Quote, null),
            TdsQuoteOutcome.Refused => (null, new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, document.SpendMoneyId, result.Detail)),
            _ => (null, new MoneyDocumentResult(MoneyDocumentOutcome.TdsUnavailable, document.SpendMoneyId,
                "The supplier's TDS details could not be read, so nothing was posted. Try again.")),
        };
    }

    /// <summary>
    /// Posts a TDS challan (TK-117): debits TDS Payable with its amount, which
    /// must be exactly what is still undeposited for its section and month,
    /// and marks those deductions paid by it. Interest and late fees are paid
    /// as ordinary payments, so the register ties to the challan to the rupee.
    /// </summary>
    private async Task<MoneyDocumentResult> PostChallanAsync(SpendMoney document, CancellationToken ct)
    {
        long id = document.SpendMoneyId;

        if (_tds is null || document.ChallanSectionCode is null || document.ChallanMonth is not DateOnly month)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, id,
                "A challan needs the section and the month whose deductions it pays.");
        }

        if (document.ExchangeRate != 1m)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, id,
                "TDS is paid to the government in the branch's own currency.");
        }

        if (await MoneyPostingRepository.CheckPeriodAsync(_ledger, document.TransactionDate, ct) is { } closed)
        {
            return closed with { DocumentId = id };
        }

        decimal due = await _tds.OutstandingForAsync(document.ChallanSectionCode, month, ct);
        if (due == 0m)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, id,
                $"Nothing deducted under {document.ChallanSectionCode} in {month:MMMM yyyy} is waiting to be deposited.");
        }

        if (due != document.Amount)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, id,
                $"{due:0.00} deducted under {document.ChallanSectionCode} in {month:MMMM yyyy} is waiting to be deposited, "
                    + $"and a challan pays exactly that. Enter {due:0.00}; pay any interest or late fee as a separate payment.");
        }

        long? bankAccountId = await ResolveBankAsync(document.BankAccountId, ct);
        if (bankAccountId is null)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.BankAccountUnusable, id,
                "That account has no ledger account behind it, so the challan cannot be posted.");
        }

        string memo = $"TDS challan {document.ChallanNo} — {document.ChallanSectionCode}, {month:MMM yyyy}";
        List<LedgerPostingLeg> legs =
        [
            new(MoneyPostingMapRepository.ControlLedgerType, ChallanLedgerSource, 1, TdsPayableAccount,
                AccountId: null, SubAccountReferenceType: null, SubAccountReferenceId: null, MoneyPostingMapRepository.Primary,
                DebitAmount: document.Amount, CreditAmount: 0m, memo),
            MoneyPostingRepository.Bank(1, ChallanLedgerSource, bankAccountId.Value, debit: 0m, credit: document.Amount, memo),
        ];

        LedgerPostOutcome posted = await _ledger.PostAsync(
            new LedgerPosting(TypeCode, id, document.TransactionDate, document.CurrencyCode,
                document.ExchangeRate, document.ContactId, legs),
            ct);

        if (posted != LedgerPostOutcome.Posted)
        {
            return new MoneyDocumentResult(
                MoneyDocumentOutcome.PostingRefused, id,
                "The ledger did not accept the challan, so nothing was posted.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        // A deduction posted since the check would make the challan pay more
        // than its amount; the mark is refused and rolled back rather than
        // leaving a deduction marked paid by money that never covered it.
        int marked = await _tds.MarkDepositedAsync(id, document.ChallanSectionCode, month, ct);
        decimal covered = await _db.TdsEntries.Where(e => e.ChallanSpendMoneyId == id).SumAsync(e => e.TdsAmount, ct);
        if (marked == 0 || covered != document.Amount)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, id,
                "A deduction was recorded under this section and month while the challan was posting. Check the amount and post again.");
        }

        NumberAllocation allocation = await _numbers.NextAsync(TypeCode, document.TransactionDate, ct);

        document.TransactionNo = allocation.Code;
        document.Status = MoneyDocumentStatus.Posted;
        document.PostedAt = _clock.GetUtcNow();
        document.PostedBy = _user.UserId;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary><c>mst.LedgerSources</c> 1 — an ordinary document posting, which a challan is.</summary>
    private const int ChallanLedgerSource = 1;

    /// <summary>
    /// Copies the challan fields onto a draft (TK-117). A challan's month is
    /// kept as its first day; an ordinary payment keeps none of them.
    /// </summary>
    private static void ApplyChallan(SpendMoney document, SaveMoneyDocumentRequest request)
    {
        document.IsTdsChallan = request.IsTdsChallan;
        document.ChallanNo = request.IsTdsChallan ? request.ChallanNo?.Trim() : null;
        document.BsrCode = request.IsTdsChallan ? request.BsrCode?.Trim() : null;
        document.ChallanSectionCode = request.IsTdsChallan ? request.ChallanSectionCode?.Trim() : null;
        document.ChallanMonth = request.IsTdsChallan && request.ChallanMonth is DateOnly month
            ? TdsWithholdingRepository.MonthStart(month)
            : null;
    }

    /// <summary>
    /// Withdraws a posted payment: the ledger rows go, the document and its
    /// number stay. A void is refused into a closed period for the same reason a
    /// post is — it changes what the ledger says about that date.
    /// </summary>
    public async Task<MoneyDocumentResult> VoidAsync(
        long id, VoidMoneyDocumentRequest request, CancellationToken ct)
    {
        SpendMoney? document = await _db.SpendMoney.FirstOrDefaultAsync(d => d.SpendMoneyId == id, ct);

        if (document is null)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotFound);
        }

        if (document.Status != MoneyDocumentStatus.Posted)
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.NotPosted, id);
        }

        // An expense charged on to a client stands behind that invoice's line (TK-106).
        if (await _db.SpendMoneyDetails.AnyAsync(l => l.SpendMoneyId == id && l.BilledInvoiceId != null, ct))
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.BilledToClient, id,
                "An expense on this payment has been charged on to a client. Void that invoice first.");
        }

        MoneyDocumentResult? closed =
            await MoneyPostingRepository.CheckPeriodAsync(_ledger, document.TransactionDate, ct);

        if (closed is not null)
        {
            return closed with { DocumentId = id };
        }

        // TDS deducted on the payment and already paid over freezes it (TK-117).
        if (_tds is not null && await _tds.IsDepositedAsync(TypeCode, id, ct))
        {
            return new MoneyDocumentResult(MoneyDocumentOutcome.TdsDeposited, id,
                "The TDS deducted on this payment has been paid to the government on a challan. Void the challan first.");
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
                "The ledger rows could not be withdrawn, so the payment is unchanged.");
        }

        document.Status = MoneyDocumentStatus.Void;
        document.VoidedAt = _clock.GetUtcNow();
        document.VoidedBy = _user.UserId;
        document.VoidReason = request.Reason;

        // A voided challan gives its deductions back to the undeposited pool; a
        // voided payment leaves the register (TK-117).
        if (_tds is not null)
        {
            if (document.IsTdsChallan)
            {
                await _tds.ReleaseChallanAsync(id, ct);
            }
            else
            {
                await _tds.RemoveAsync(TypeCode, id, ct);
            }
        }

        await _db.SaveChangesAsync(ct);

        return new MoneyDocumentResult(MoneyDocumentOutcome.Ok, id);
    }

    /// <summary>
    /// The bank account's own GL account. Null when the account is not in this
    /// branch, or when provisioning left it unlinked — a payment out of an
    /// account with no ledger identity has nowhere to credit.
    /// </summary>
    private async Task<long?> ResolveBankAsync(long bankAccountId, CancellationToken ct) =>
        await _db.BankAccounts
            .Where(a => a.BankAccountId == bankAccountId && a.IsActive)
            .Select(a => a.LedgerAccountId)
            .FirstOrDefaultAsync(ct);

    private static MoneyDocumentResult? Validate(SaveMoneyDocumentRequest request)
    {
        // A challan pays TDS Payable and nothing else (TK-117).
        if (request.IsTdsChallan)
        {
            if (request.Lines.Count > 0)
            {
                return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, 0,
                    "A TDS challan has no lines: it pays what was deducted under one section in one month.");
            }

            if (string.IsNullOrWhiteSpace(request.ChallanSectionCode) || request.ChallanMonth is null)
            {
                return new MoneyDocumentResult(MoneyDocumentOutcome.TdsRefused, 0,
                    "Choose the section and the month whose deductions the challan pays.");
            }

            return null;
        }

        foreach (SaveMoneyLineRequest line in request.Lines)
        {
            if (MoneyPostingMapRepository.ForSpend(line.LedgerSourceId) is null)
            {
                return new MoneyDocumentResult(
                    MoneyDocumentOutcome.UnknownLedgerSource, 0,
                    "One of the lines is for something money cannot leave under.");
            }
        }

        // What the lines say about the documents they settle: the right kind of
        // document for what each line is for, and each document named once.
        return MoneyAllocationRepository.Check(request.Lines, spending: true);
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

    private static IEnumerable<SpendMoneyDetail> BuildLines(
        SpendMoney document, List<SaveMoneyLineRequest> lines) =>
        lines.Select((line, index) => new SpendMoneyDetail
        {
            SpendMoneyId = document.SpendMoneyId,
            LineNumber = index + 1,
            LedgerSourceId = line.LedgerSourceId,
            MappingTransactionTypeCode = line.MappingTransactionTypeCode,
            MappingTransactionId = line.MappingTransactionId,
            Amount = line.Amount,
            AmountBase = MoneyPostingRepository.Base(line.Amount, document.ExchangeRate),
            LineMemo = line.LineMemo,
            ProjectId = line.ProjectId,

            // Only a line with a project can be charged on to its client (TK-106).
            IsBillable = line.IsBillable && line.ProjectId is not null,
            MarkupPercent = line.IsBillable && line.ProjectId is not null ? line.MarkupPercent : null,
        });

    private IQueryable<MoneyDocumentListItem> Summarize(IQueryable<SpendMoney> documents) =>
        from d in documents
        join a in _db.BankAccounts on d.BankAccountId equals a.BankAccountId
        select new MoneyDocumentListItem
        {
            DocumentId = d.SpendMoneyId,
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
            IsTdsChallan = d.IsTdsChallan,
            ChallanNo = d.ChallanNo,
            BsrCode = d.BsrCode,
            ChallanSectionCode = d.ChallanSectionCode,
            ChallanMonth = d.ChallanMonth,
        };
}
