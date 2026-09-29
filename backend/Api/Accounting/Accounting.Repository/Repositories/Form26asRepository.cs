using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository.Repositories;

/// <summary>
/// Form 26AS imports (TK-117, D-33): read the statement, match it against the
/// TDS receipts record customers withholding for that year, and keep the file
/// with its result.
/// </summary>
public sealed class Form26asRepository
{
    private readonly AccountingDbContext _db;
    private readonly IOrgIdentityProvider? _identity;

    public Form26asRepository(AccountingDbContext db, IOrgIdentityProvider? identity = null)
    {
        _db = db;
        _identity = identity;
    }

    public async Task<Form26asImportResult> ImportAsync(string fileName, string text, CancellationToken ct)
    {
        Form26asStatement statement;
        try
        {
            statement = Form26asFileRepository.Parse(text);
        }
        catch (Form26asFormatException refused)
        {
            return new Form26asImportResult(Form26asImportOutcome.Unreadable, Detail: refused.Message);
        }

        // A GSTIN carries the holder's PAN in characters 3 to 12, which is how a
        // branch knows its own PAN without a column for it.
        if (statement.Pan is not null && _identity is not null
            && await _identity.GetIdentityAsync(ct) is { Gstin: { Length: 15 } gstin }
            && !string.Equals(gstin.Substring(2, 10), statement.Pan, StringComparison.OrdinalIgnoreCase))
        {
            return new Form26asImportResult(Form26asImportOutcome.WrongPan,
                Detail: $"This statement is for PAN {statement.Pan}, and this branch's GSTIN is registered to {gstin.Substring(2, 10).ToUpperInvariant()}.");
        }

        List<BooksTds> books = await _db.TdsEntries.AsNoTracking()
            .Where(e => e.Direction == TdsDirection.Suffered && e.FinancialYear == statement.FinancialYear && e.TdsAmount > 0)
            .Select(e => new BooksTds(e.TransactionId, e.DocumentNo, e.Tan, e.DocumentDate, e.TdsAmount, e.SectionCode))
            .ToListAsync(ct);

        Form26asMatchResult matched = Form26asMatcherRepository.Match(statement.Transactions, books);

        Form26asImport import = new()
        {
            FinancialYear = statement.FinancialYear,
            Pan = statement.Pan,
            FileName = fileName.Length <= 260 ? fileName : fileName[..260],
            RawText = text,
            MatchedCount = matched.Lines.Count(l => l.MatchState == Form26asMatchState.Matched),
            DifferenceCount = matched.Lines.Count(l => l.MatchState == Form26asMatchState.AmountDiffers),
            MissingInBooksCount = matched.Lines.Count(l => l.MatchState == Form26asMatchState.MissingInBooks),
            MissingIn26asCount = matched.Lines.Count(l => l.MatchState == Form26asMatchState.MissingIn26as),
            CreditAtRisk = matched.CreditAtRisk,
            Lines = [.. matched.Lines],
        };

        _db.Form26asImports.Add(import);
        await _db.SaveChangesAsync(ct);

        return new Form26asImportResult(Form26asImportOutcome.Ok, import.Form26asImportId);
    }

    public Task<List<Form26asImportSummary>> ListAsync(CancellationToken ct) =>
        _db.Form26asImports.AsNoTracking()
            .OrderByDescending(i => i.Form26asImportId)
            .Select(i => new Form26asImportSummary
            {
                Form26asImportId = i.Form26asImportId,
                FinancialYear = i.FinancialYear,
                Pan = i.Pan,
                FileName = i.FileName,
                ImportedAt = i.CreatedAt,
                MatchedCount = i.MatchedCount,
                DifferenceCount = i.DifferenceCount,
                MissingInBooksCount = i.MissingInBooksCount,
                MissingIn26asCount = i.MissingIn26asCount,
                CreditAtRisk = i.CreditAtRisk,
            })
            .ToListAsync(ct);

    public async Task<Form26asImportDetail?> GetAsync(long id, CancellationToken ct)
    {
        Form26asImport? import = await _db.Form26asImports.AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Form26asImportId == id, ct);

        if (import is null)
        {
            return null;
        }

        return new Form26asImportDetail
        {
            Form26asImportId = import.Form26asImportId,
            FinancialYear = import.FinancialYear,
            Pan = import.Pan,
            FileName = import.FileName,
            ImportedAt = import.CreatedAt,
            MatchedCount = import.MatchedCount,
            DifferenceCount = import.DifferenceCount,
            MissingInBooksCount = import.MissingInBooksCount,
            MissingIn26asCount = import.MissingIn26asCount,
            CreditAtRisk = import.CreditAtRisk,
            Lines = [.. import.Lines
                .OrderBy(l => l.MatchState)
                .ThenBy(l => l.DeductorTan, StringComparer.Ordinal)
                .ThenBy(l => l.TransactionDate)
                .Select(l => new Form26asLineView
                {
                    Form26asLineId = l.Form26asLineId,
                    MatchState = l.MatchState.ToString(),
                    DeductorTan = l.DeductorTan,
                    DeductorName = l.DeductorName,
                    SectionCode = l.SectionCode,
                    TransactionDate = l.TransactionDate,
                    StatementAmountPaid = l.StatementAmountPaid,
                    StatementTds = l.StatementTds,
                    BooksTds = l.BooksTds,
                    ReceiveMoneyId = l.ReceiveMoneyId,
                    DocumentNo = l.DocumentNo,
                    Note = l.Note,
                })],
        };
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        Form26asImport? import = await _db.Form26asImports.FirstOrDefaultAsync(i => i.Form26asImportId == id, ct);
        if (import is null)
        {
            return false;
        }

        _db.Form26asImports.Remove(import);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
