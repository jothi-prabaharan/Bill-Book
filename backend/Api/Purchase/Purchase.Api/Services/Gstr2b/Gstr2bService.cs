using Microsoft.EntityFrameworkCore;
using Purchase.Entity.Enums;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Purchase.Api.Services.Gstr2b;

/// <summary>
/// GSTR-2B reconciliation (TK-116): import a month's file, match it against the
/// purchase register, and keep what was found. Matching is <see cref="Gstr2bMatcher"/>'s;
/// this reads the books and stores the result.
/// </summary>
public sealed class Gstr2bService
{
    private readonly PurchaseDbContext _db;
    private readonly IOrgIdentityProvider? _identity;

    public Gstr2bService(PurchaseDbContext db, IOrgIdentityProvider? identity = null)
    {
        _db = db;
        _identity = identity;
    }

    public async Task<Gstr2bImportResult> ImportAsync(string fileName, string json, CancellationToken ct)
    {
        Gstr2bDocument document;
        try
        {
            document = Gstr2bFile.Parse(json);
        }
        catch (Gstr2bFormatException ex)
        {
            return new Gstr2bImportResult(Gstr2bImportOutcome.Unreadable, Detail: ex.Message);
        }

        // A 2B generated for another registration would match nothing and
        // report every bill missing, which reads as a disaster rather than as
        // the wrong file.
        if (_identity is not null && document.RecipientGstin is { Length: 15 } recipient
            && await _identity.GetIdentityAsync(ct) is { Gstin: { Length: 15 } own }
            && !string.Equals(recipient, own, StringComparison.OrdinalIgnoreCase))
        {
            return new Gstr2bImportResult(
                Gstr2bImportOutcome.WrongGstin,
                Detail: $"This GSTR-2B is for {recipient}, and this branch is {own.ToUpperInvariant()}.");
        }

        HashSet<string> suppliers = [.. document.Invoices.Select(i => i.SupplierGstin)];

        List<BooksBill> candidates = await BillsAsync(r => r.ContactGstin != null && suppliers.Contains(r.ContactGstin.ToUpper()), ct);
        List<BooksBill> period = await BillsAsync(
            r => r.ContactGstin != null && r.SupplierInvoiceDate >= document.From && r.SupplierInvoiceDate <= document.To, ct);

        Gstr2bMatchResult result = Gstr2bMatcher.Match(document.Invoices, candidates, period);

        Gstr2bImport import = new()
        {
            ReturnPeriod = document.ReturnPeriod,
            PeriodFrom = document.From,
            PeriodTo = document.To,
            RecipientGstin = document.RecipientGstin,
            FileName = fileName.Length <= 200 ? fileName : fileName[..200],
            RawJson = json,
            MatchedCount = result.Count(Gstr2bMatchState.Matched),
            MismatchedCount = result.Count(Gstr2bMatchState.Mismatched),
            MissingInBooksCount = result.Count(Gstr2bMatchState.MissingInBooks),
            MissingIn2bCount = result.Count(Gstr2bMatchState.MissingIn2b),
            ItcAtRisk = result.ItcAtRisk,
            Lines = [.. result.Lines],
        };

        _db.Gstr2bImports.Add(import);
        await _db.SaveChangesAsync(ct);

        return new Gstr2bImportResult(Gstr2bImportOutcome.Ok, import.Gstr2bImportId);
    }

    public async Task<List<Gstr2bImportView>> ListAsync(CancellationToken ct) =>
        await _db.Gstr2bImports.AsNoTracking()
            .OrderByDescending(i => i.PeriodFrom)
            .ThenByDescending(i => i.Gstr2bImportId)
            .Select(i => new Gstr2bImportView
            {
                Gstr2bImportId = i.Gstr2bImportId,
                ReturnPeriod = i.ReturnPeriod,
                PeriodFrom = i.PeriodFrom,
                PeriodTo = i.PeriodTo,
                FileName = i.FileName,
                ImportedAt = i.CreatedAt,
                MatchedCount = i.MatchedCount,
                MismatchedCount = i.MismatchedCount,
                MissingInBooksCount = i.MissingInBooksCount,
                MissingIn2bCount = i.MissingIn2bCount,
                ItcAtRisk = i.ItcAtRisk,
            })
            .ToListAsync(ct);

    public async Task<Gstr2bImportDetail?> GetAsync(long id, CancellationToken ct)
    {
        Gstr2bImportDetail? detail = await _db.Gstr2bImports.AsNoTracking()
            .Where(i => i.Gstr2bImportId == id)
            .Select(i => new Gstr2bImportDetail
            {
                Gstr2bImportId = i.Gstr2bImportId,
                ReturnPeriod = i.ReturnPeriod,
                PeriodFrom = i.PeriodFrom,
                PeriodTo = i.PeriodTo,
                FileName = i.FileName,
                ImportedAt = i.CreatedAt,
                MatchedCount = i.MatchedCount,
                MismatchedCount = i.MismatchedCount,
                MissingInBooksCount = i.MissingInBooksCount,
                MissingIn2bCount = i.MissingIn2bCount,
                ItcAtRisk = i.ItcAtRisk,
            })
            .FirstOrDefaultAsync(ct);

        if (detail is null)
        {
            return null;
        }

        detail.Lines = await _db.Gstr2bLines.AsNoTracking()
            .Where(l => l.Gstr2bImportId == id)
            .OrderBy(l => l.MatchState)
            .ThenBy(l => l.SupplierGstin)
            .ThenBy(l => l.InvoiceDate)
            .Select(l => new Gstr2bLineView
            {
                Gstr2bLineId = l.Gstr2bLineId,
                MatchState = l.MatchState,
                SupplierGstin = l.SupplierGstin,
                SupplierName = l.SupplierName,
                InvoiceNo = l.InvoiceNo,
                InvoiceDate = l.InvoiceDate,
                ReverseCharge = l.ReverseCharge,
                ItcAvailable = l.ItcAvailable,
                TwoBTaxable = l.TwoBTaxable,
                TwoBTax = l.TwoBTax,
                BooksTaxable = l.BooksTaxable,
                BooksTax = l.BooksTax,
                BillId = l.BillId,
                BillNo = l.BillNo,
                Note = l.Note,
            })
            .ToListAsync(ct);

        return detail;
    }

    /// <summary>Removes an import and its lines. Nothing else depends on it; the books are untouched.</summary>
    public async Task<bool> DeleteAsync(long id, CancellationToken ct) =>
        await _db.Gstr2bImports.Where(i => i.Gstr2bImportId == id).ExecuteDeleteAsync(ct) == 1;

    /// <summary>Posted bills, one per bill, totalled from their register rows in base currency.</summary>
    private async Task<List<BooksBill>> BillsAsync(
        System.Linq.Expressions.Expression<Func<PurchaseRegister, bool>> filter, CancellationToken ct)
    {
        var rows = await _db.PurchaseRegister.AsNoTracking()
            .Where(r => r.TransactionTypeCode == "BIL")
            .Where(filter)
            .GroupBy(r => new { r.SourceId, r.DocumentNo, r.ContactGstin, r.SupplierInvoiceNo, r.SupplierInvoiceDate })
            .Select(g => new
            {
                g.Key.SourceId,
                g.Key.DocumentNo,
                g.Key.ContactGstin,
                g.Key.SupplierInvoiceNo,
                g.Key.SupplierInvoiceDate,
                Taxable = g.Sum(r => r.TaxableAmountBase),
                Tax = g.Sum(r => r.TaxAmountBase),
                EligibleTax = g.Where(r => r.ItcEligibility == ItcEligibility.Eligible).Sum(r => r.TaxAmountBase),
                ReverseCharge = g.Any(r => r.ReverseCharge),
            })
            .ToListAsync(ct);

        return
        [
            .. rows.Select(r => new BooksBill(
                r.SourceId, r.DocumentNo, r.ContactGstin!, r.SupplierInvoiceNo, r.SupplierInvoiceDate,
                r.Taxable, r.Tax, r.EligibleTax, r.ReverseCharge)),
        ];
    }
}
