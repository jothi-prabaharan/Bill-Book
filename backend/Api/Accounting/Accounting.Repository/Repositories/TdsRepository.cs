using Shared.Kernel.Audit;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.Tax;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The TDS register (TK-117): what a document owes, the row it leaves behind,
/// and what a challan pays over.
///
/// A quote is read-only. The row is written by whoever posts the document, in
/// the posting's transaction — the ledger for a bill, the spend or receive
/// money document for its own — so the register and the ledger cannot
/// disagree about whether a document happened.
/// </summary>
public sealed class TdsRepository
{
    private readonly AccountingDbContext _db;
    private readonly ITdsProfileClient _profiles;

    public TdsRepository(AccountingDbContext db, ITdsProfileClient profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    /// <summary>
    /// What TDS a document owes. The document's own earlier row is left out of
    /// the year's history, so a re-post after an edit is priced afresh rather
    /// than counted twice.
    /// </summary>
    public async Task<TdsQuoteResult> QuoteAsync(TdsQuoteRequest request, CancellationToken ct)
    {
        if (await _profiles.ProfileAsync(request.ContactId, ct) is not { } profile)
        {
            return new TdsQuoteResult(TdsQuoteOutcome.Unavailable, null, null);
        }

        if (!profile.IsTdsApplicable || string.IsNullOrWhiteSpace(profile.TdsSection))
        {
            return new TdsQuoteResult(TdsQuoteOutcome.Ok, new TdsQuote { Applies = false, Note = "TDS is off for this contact." }, null);
        }

        TdsSection? section = await new TdsSectionRepository(_db).InForceAsync(profile.TdsSection, request.Date, ct);
        if (section is null)
        {
            return new TdsQuoteResult(TdsQuoteOutcome.Refused, null,
                $"The contact's TDS section {profile.TdsSection} has no row in force on {request.Date:dd MMM yyyy}. "
                    + "Add one under TDS sections, or change the contact's section.");
        }

        int year = TdsCalculationRepository.FinancialYearOf(request.Date);
        string code = section.SectionCode;
        string type = request.TransactionTypeCode.ToUpperInvariant();

        List<TdsHistoryRow> history = await _db.TdsEntries.AsNoTracking()
            .Where(e => e.Direction == TdsDirection.Deducted
                && e.ContactId == request.ContactId
                && e.SectionCode == code
                && e.FinancialYear == year
                && !(e.TransactionTypeCode == type && e.TransactionId == request.TransactionId))
            .Select(e => new TdsHistoryRow(e.Kind, e.Amount, e.CoveredByAdvance, e.TdsBase, e.CertificateNo, e.CertificateBase))
            .ToListAsync(ct);

        TdsCertificate? certificate = profile is
        {
            CertificateNo: { Length: > 0 } number,
            CertificateRate: decimal rate,
            CertificateValidFrom: DateOnly from,
            CertificateValidTo: DateOnly to,
            CertificateLimit: decimal limit,
        }
            ? new TdsCertificate(number, rate, from, to, limit)
            : null;

        TdsEntryKind kind = request.Kind == (int)TdsEntryKind.Advance ? TdsEntryKind.Advance : TdsEntryKind.Credit;

        TdsComputation computed = TdsCalculationRepository.Compute(
            section, new TdsDeductee(profile.Pan, certificate), history, kind, request.Amount, request.Date);

        return new TdsQuoteResult(TdsQuoteOutcome.Ok, new TdsQuote
        {
            Applies = true,
            Kind = (int)kind,
            SectionCode = code,
            Pan = TdsCalculationRepository.IsValidPan(profile.Pan) ? profile.Pan!.Trim().ToUpperInvariant() : null,
            DeducteeKind = (int)computed.DeducteeKind,
            FinancialYear = year,
            Amount = computed.Amount,
            CoveredByAdvance = computed.CoveredByAdvance,
            TdsBase = computed.TdsBase,
            Rate = computed.Rate,
            CertificateNo = computed.CertificateNo,
            CertificateBase = computed.CertificateBase,
            CertificateRate = computed.CertificateRate,
            TdsAmount = computed.TdsAmount,
            Note = computed.Note,
        }, null);
    }

    /// <summary>Whether this document's TDS has been paid over on a challan, which freezes it.</summary>
    public Task<bool> IsDepositedAsync(string typeCode, long transactionId, CancellationToken ct)
    {
        string type = typeCode.ToUpperInvariant();
        return _db.TdsEntries.AnyAsync(
            e => e.TransactionTypeCode == type && e.TransactionId == transactionId && e.ChallanSpendMoneyId != null, ct);
    }

    /// <summary>
    /// Replaces the document's row with one from a quote. The caller has
    /// checked it is not deposited, and owns the transaction.
    /// </summary>
    public async Task WriteDeductionAsync(
        string typeCode, long transactionId, string? documentNo, DateOnly date, long contactId, TdsQuote quote, CancellationToken ct)
    {
        await RemoveAsync(typeCode, transactionId, ct);

        _db.TdsEntries.Add(new TdsEntry
        {
            Direction = TdsDirection.Deducted,
            Kind = quote.Kind == (int)TdsEntryKind.Advance ? TdsEntryKind.Advance : TdsEntryKind.Credit,
            TransactionTypeCode = typeCode.ToUpperInvariant(),
            TransactionId = transactionId,
            DocumentNo = Trim(documentNo, 50),
            DocumentDate = date,
            FinancialYear = TdsCalculationRepository.FinancialYearOf(date),
            ContactId = contactId,
            SectionCode = quote.SectionCode,
            Pan = quote.Pan,
            DeducteeKind = Enum.IsDefined(typeof(TdsDeducteeKind), quote.DeducteeKind) ? (TdsDeducteeKind)quote.DeducteeKind : null,
            Amount = quote.Amount,
            CoveredByAdvance = quote.CoveredByAdvance,
            TdsBase = quote.TdsBase,
            Rate = quote.Rate,
            CertificateNo = quote.CertificateNo,
            CertificateBase = quote.CertificateBase,
            CertificateRate = quote.CertificateRate,
            TdsAmount = quote.TdsAmount,
            Note = Trim(quote.Note, 300),
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Records TDS a customer withheld from a receipt.</summary>
    public async Task WriteSufferedAsync(
        long receiveMoneyId, string? documentNo, DateOnly date, long contactId, string? sectionCode, string? tan,
        decimal received, decimal tds, CancellationToken ct)
    {
        await RemoveAsync("RCM", receiveMoneyId, ct);

        _db.TdsEntries.Add(new TdsEntry
        {
            Direction = TdsDirection.Suffered,
            Kind = TdsEntryKind.Receipt,
            TransactionTypeCode = "RCM",
            TransactionId = receiveMoneyId,
            DocumentNo = Trim(documentNo, 50),
            DocumentDate = date,
            FinancialYear = TdsCalculationRepository.FinancialYearOf(date),
            ContactId = contactId,
            SectionCode = sectionCode,
            Tan = tan?.Trim().ToUpperInvariant(),
            Amount = received + tds,
            TdsBase = received + tds,
            Rate = received + tds == 0 ? 0m : Math.Round(tds * 100m / (received + tds), 3),
            TdsAmount = tds,
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string typeCode, long transactionId, CancellationToken ct)
    {
        string type = typeCode.ToUpperInvariant();

        await _db.TdsEntries
            .Where(e => e.TransactionTypeCode == type && e.TransactionId == transactionId)
            .ExecuteDeleteLoggedAsync(_db, ct);

        // ExecuteDelete bypasses the change tracker; anything it had read is gone.
        foreach (var stale in _db.ChangeTracker.Entries<TdsEntry>()
            .Where(e => e.Entity.TransactionTypeCode == type && e.Entity.TransactionId == transactionId)
            .ToList())
        {
            stale.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Undeposited deductions by section and month, with the date each falls
    /// due: the 7th of the following month, or 30 April for March.
    /// </summary>
    public async Task<List<TdsOutstandingView>> OutstandingAsync(CancellationToken ct)
    {
        var rows = await _db.TdsEntries.AsNoTracking()
            .Where(e => e.Direction == TdsDirection.Deducted && e.TdsAmount > 0 && e.ChallanSpendMoneyId == null)
            .Select(e => new { e.SectionCode, e.DocumentDate, e.TdsAmount })
            .ToListAsync(ct);

        return [.. rows
            .GroupBy(r => (Section: r.SectionCode ?? string.Empty, Month: new DateOnly(r.DocumentDate.Year, r.DocumentDate.Month, 1)))
            .OrderBy(g => g.Key.Month).ThenBy(g => g.Key.Section, StringComparer.Ordinal)
            .Select(g => new TdsOutstandingView
            {
                SectionCode = g.Key.Section,
                Month = g.Key.Month,
                TdsAmount = g.Sum(r => r.TdsAmount),
                Deductions = g.Count(),
                DueDate = DueDate(g.Key.Month),
            })];
    }

    public static DateOnly DueDate(DateOnly month) =>
        month.Month == 3 ? new DateOnly(month.Year, 4, 30) : month.AddMonths(1).AddDays(6);

    /// <summary>What a challan for this section and month would have to pay.</summary>
    public Task<decimal> OutstandingForAsync(string sectionCode, DateOnly month, CancellationToken ct)
    {
        (DateOnly from, DateOnly to) = MonthOf(month);
        string code = sectionCode.Trim().ToUpperInvariant();
        return _db.TdsEntries
            .Where(e => e.Direction == TdsDirection.Deducted && e.ChallanSpendMoneyId == null
                && e.SectionCode!.ToUpper() == code && e.DocumentDate >= from && e.DocumentDate <= to)
            .SumAsync(e => e.TdsAmount, ct);
    }

    /// <summary>Marks the section's undeposited deductions of the month as paid by this challan.</summary>
    public Task<int> MarkDepositedAsync(long spendMoneyId, string sectionCode, DateOnly month, CancellationToken ct)
    {
        (DateOnly from, DateOnly to) = MonthOf(month);
        string code = sectionCode.Trim().ToUpperInvariant();
        return _db.TdsEntries
            .Where(e => e.Direction == TdsDirection.Deducted && e.ChallanSpendMoneyId == null
                && e.SectionCode!.ToUpper() == code && e.DocumentDate >= from && e.DocumentDate <= to)
            .ExecuteUpdateLoggedAsync(_db, u => u.SetProperty(e => e.ChallanSpendMoneyId, spendMoneyId), ct);
    }

    /// <summary>A voided challan gives its deductions back to the undeposited pool.</summary>
    public Task<int> ReleaseChallanAsync(long spendMoneyId, CancellationToken ct) =>
        _db.TdsEntries
            .Where(e => e.ChallanSpendMoneyId == spendMoneyId)
            .ExecuteUpdateLoggedAsync(_db, u => u.SetProperty(e => e.ChallanSpendMoneyId, (long?)null), ct);

    /// <summary>The register for a year and a side, newest first.</summary>
    public async Task<List<TdsEntryView>> ListAsync(int financialYear, TdsDirection direction, CancellationToken ct)
    {
        var rows = await (
            from e in _db.TdsEntries.AsNoTracking()
            where e.FinancialYear == financialYear && e.Direction == direction
            join c in _db.SpendMoney on e.ChallanSpendMoneyId equals c.SpendMoneyId into challans
            from c in challans.DefaultIfEmpty()
            orderby e.DocumentDate descending, e.TdsEntryId descending
            select new TdsEntryView
            {
                TdsEntryId = e.TdsEntryId,
                Direction = e.Direction.ToString(),
                Kind = e.Kind.ToString(),
                TransactionTypeCode = e.TransactionTypeCode,
                TransactionId = e.TransactionId,
                DocumentNo = e.DocumentNo,
                DocumentDate = e.DocumentDate,
                FinancialYear = e.FinancialYear,
                ContactId = e.ContactId,
                SectionCode = e.SectionCode,
                Pan = e.Pan,
                Tan = e.Tan,
                Amount = e.Amount,
                TdsBase = e.TdsBase,
                Rate = e.Rate,
                CertificateNo = e.CertificateNo,
                TdsAmount = e.TdsAmount,
                ChallanSpendMoneyId = e.ChallanSpendMoneyId,
                ChallanNo = c == null ? null : c.ChallanNo,
                Note = e.Note,
            }).ToListAsync(ct);

        return rows;
    }

    /// <summary>The register row for one document, for its screen.</summary>
    public Task<TdsEntryView?> ForDocumentAsync(string typeCode, long transactionId, CancellationToken ct)
    {
        string type = typeCode.ToUpperInvariant();
        return _db.TdsEntries.AsNoTracking()
            .Where(e => e.TransactionTypeCode == type && e.TransactionId == transactionId)
            .Select(e => new TdsEntryView
            {
                TdsEntryId = e.TdsEntryId,
                Direction = e.Direction.ToString(),
                Kind = e.Kind.ToString(),
                TransactionTypeCode = e.TransactionTypeCode,
                TransactionId = e.TransactionId,
                DocumentNo = e.DocumentNo,
                DocumentDate = e.DocumentDate,
                FinancialYear = e.FinancialYear,
                ContactId = e.ContactId,
                SectionCode = e.SectionCode,
                Pan = e.Pan,
                Tan = e.Tan,
                Amount = e.Amount,
                TdsBase = e.TdsBase,
                Rate = e.Rate,
                CertificateNo = e.CertificateNo,
                TdsAmount = e.TdsAmount,
                ChallanSpendMoneyId = e.ChallanSpendMoneyId,
                Note = e.Note,
            })
            .FirstOrDefaultAsync(ct);
    }

    private static (DateOnly From, DateOnly To) MonthOf(DateOnly month)
    {
        DateOnly from = new(month.Year, month.Month, 1);
        return (from, from.AddMonths(1).AddDays(-1));
    }

    private static string? Trim(string? text, int max) =>
        text is null ? null : text.Length <= max ? text : text[..max];
}
