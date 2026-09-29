using Accounting.Entity.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;

namespace Accounting.Repository.Repositories;

/// <summary>
/// Gathers a quarter's deductions and the challans that paid them for the 26Q
/// export (TK-117). A challan belongs to the quarter of the deductions it
/// paid, so March's deductions deposited in April go out with the fourth
/// quarter where the return wants them.
/// </summary>
public sealed class Form26qRepository
{
    private readonly AccountingDbContext _db;
    private readonly IContactNameLookup _names;

    public Form26qRepository(AccountingDbContext db, IContactNameLookup names)
    {
        _db = db;
        _names = names;
    }

    public async Task<Form26qExport> ExportAsync(int financialYear, int quarter, CancellationToken ct)
    {
        (DateOnly from, DateOnly to) = Form26qExportRepository.QuarterOf(financialYear, quarter);

        var deductions = await _db.TdsEntries.AsNoTracking()
            .Where(e => e.Direction == TdsDirection.Deducted && e.TdsAmount > 0
                && e.DocumentDate >= from && e.DocumentDate <= to)
            .Select(e => new
            {
                e.ChallanSpendMoneyId,
                e.SectionCode,
                e.Pan,
                e.ContactId,
                e.DocumentDate,
                e.Amount,
                e.TdsAmount,
                e.Rate,
                e.CertificateNo,
                e.CertificateRate,
                e.DeducteeKind,
            })
            .ToListAsync(ct);

        List<long> challanIds = [.. deductions.Where(d => d.ChallanSpendMoneyId != null).Select(d => d.ChallanSpendMoneyId!.Value).Distinct()];

        var challans = await _db.SpendMoney.AsNoTracking()
            .Where(s => challanIds.Contains(s.SpendMoneyId))
            .Select(s => new { s.SpendMoneyId, s.ChallanSectionCode, s.Amount, s.BsrCode, s.ChallanNo, s.TransactionDate })
            .ToListAsync(ct);

        IReadOnlyDictionary<long, NamedRef> names = await _names.ResolveAsync([.. deductions.Select(d => d.ContactId).Distinct()], ct);

        return Form26qExportRepository.Build(
            [.. challans.Select(c => new Form26qChallan(c.SpendMoneyId, c.ChallanSectionCode ?? string.Empty, c.Amount, c.BsrCode, c.ChallanNo, c.TransactionDate))],
            [.. deductions.Select(d => new Form26qDeduction(
                d.ChallanSpendMoneyId,
                d.SectionCode ?? string.Empty,
                d.Pan,
                names.TryGetValue(d.ContactId, out NamedRef? named) ? named.Name : $"Contact {d.ContactId}",
                d.DocumentDate,
                d.Amount,
                d.TdsAmount,
                d.CertificateNo is null ? d.Rate : d.CertificateRate ?? d.Rate,
                d.CertificateNo,
                d.DeducteeKind))]);
    }
}
