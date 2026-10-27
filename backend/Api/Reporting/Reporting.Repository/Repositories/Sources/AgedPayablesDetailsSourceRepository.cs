using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public class AgedPayablesDetailsRow
{
    /// <summary>The bill's id on a bill, and nought on any other document.</summary>
    public long BillId { get; set; }
    public string? DocumentNo { get; set; }
    public DateOnly DocumentDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }

    // Not columns: what a payment's or receipt's number is looked up by, and the unique tie-break.
    public string TypeCode { get; set; } = null!;
    public long DocumentId { get; set; }
    public string SortKey { get; set; } = null!;
}

/// <summary>
/// The Aged Payables Details for export, saved views and scheduled emails (TK-234). One row for each document still
/// open on the date, from the open documents the page and the summary use
/// (<see cref="AgedPayablesSummaryReportRepository.OpenOn"/>): the document's total and what is left of it, signed, so a
/// debit note or an unapplied payment is negative. <b>The as-at date is required</b> (D-67). What no vendor claims is
/// listed under "Not tied to a vendor", so the rows add up to the payable account.
///
/// A payment's or receipt's number is the document's own, which the ledger cannot carry, so it is filled in over the page
/// once it is in hand and cannot be filtered on.
/// </summary>
public sealed class AgedPayablesDetailsSourceRepository : ReportSource<AgedPayablesDetailsRow>
{
    private ReportingDbContext? _db;

    public override string ReportKey => "ap-aging-detail";
    public override string Title => "Aged Payables Details";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "asOf", Label = "As At", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AgedPayablesDetailsRow, string?>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<AgedPayablesDetailsRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<AgedPayablesDetailsRow, DateOnly?>("dueDate", ColumnDataType.Date, r => r.DueDate),
        ReportColumn.Of<AgedPayablesDetailsRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<AgedPayablesDetailsRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, AggregateFunction.Sum),
        ReportColumn.Of<AgedPayablesDetailsRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, AggregateFunction.Sum),
        ReportColumn.Of<AgedPayablesDetailsRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false),
    ];

    /// <summary>The number lookup needs the database once the page is in hand, and a source cannot get it then.</summary>
    protected override Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct)
    {
        _db = db;
        return Task.CompletedTask;
    }

    protected override IQueryable<AgedPayablesDetailsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly asOf = parameters.Date("asOf") ?? throw new ReportQueryException("As at date is required.");

        IQueryable<OpenReceivable> open = AgedPayablesSummaryReportRepository.OpenOn(db, asOf, null);

        IQueryable<AgedPayablesDetailsRow> named =
            from o in open
            join c in db.Contacts on o.ContactId equals (long?)c.ContactId
            select new AgedPayablesDetailsRow
            {
                BillId = o.TypeCode == "BIL" ? o.DocumentId : 0,
                DocumentNo = o.DocumentNo,
                DocumentDate = o.DocumentDate,
                DueDate = o.TypeCode == "BIL" ? o.DueDate : (DateOnly?)null,
                ContactName = c.DisplayName,
                TotalAmount = o.Original,
                AmountDue = o.Amount,
                TypeCode = o.TypeCode,
                DocumentId = o.DocumentId,
                SortKey = o.DocumentDate.DayNumber.ToString().PadLeft(7, '0') + o.TypeCode + o.DocumentId.ToString().PadLeft(12, '0'),
            };

        // What no vendor claims, as the page lists it, so the rows add up to the payable account.
        IQueryable<AgedPayablesDetailsRow> unassigned = open
            .Where(o => o.ContactId == null)
            .Select(o => new AgedPayablesDetailsRow
            {
                BillId = o.TypeCode == "BIL" ? o.DocumentId : 0,
                DocumentNo = o.DocumentNo,
                DocumentDate = o.DocumentDate,
                DueDate = o.TypeCode == "BIL" ? o.DueDate : (DateOnly?)null,
                ContactName = AgedPayablesSummaryReportRepository.UnassignedName,
                TotalAmount = o.Original,
                AmountDue = o.Amount,
                TypeCode = o.TypeCode,
                DocumentId = o.DocumentId,
                SortKey = o.DocumentDate.DayNumber.ToString().PadLeft(7, '0') + o.TypeCode + o.DocumentId.ToString().PadLeft(12, '0'),
            });

        return named.Concat(unassigned);
    }

    /// <summary>A payment or receipt is numbered by its own document; anything still without a number reads as its kind and id.</summary>
    protected override async Task FormatRowsAsync(IReadOnlyList<AgedPayablesDetailsRow> page, CancellationToken ct)
    {
        List<AgedPayablesDetailsRow> missing = [.. page.Where(r => r.DocumentNo == null)];
        if (missing.Count == 0)
        {
            return;
        }

        Dictionary<(string, long), string> numbers = [];
        if (_db is not null)
        {
            List<long> receiptIds = [.. missing.Where(r => r.TypeCode == "RCM").Select(r => r.DocumentId)];
            List<long> paymentIds = [.. missing.Where(r => r.TypeCode == "SPM").Select(r => r.DocumentId)];

            if (receiptIds.Count > 0)
            {
                foreach (MoneyNumber n in await AgedReceivablesDetailReportRepository.ReceiptNumbers(_db.ReceiveMoney, receiptIds).ToListAsync(ct))
                {
                    numbers[(n.TypeCode, n.DocumentId)] = n.Number;
                }
            }

            if (paymentIds.Count > 0)
            {
                foreach (MoneyNumber n in await AgedReceivablesDetailReportRepository.PaymentNumbers(_db.SpendMoney, paymentIds).ToListAsync(ct))
                {
                    numbers[(n.TypeCode, n.DocumentId)] = n.Number;
                }
            }
        }

        foreach (AgedPayablesDetailsRow row in missing)
        {
            row.DocumentNo = numbers.GetValueOrDefault((row.TypeCode, row.DocumentId)) ?? $"{row.TypeCode}-{row.DocumentId}";
        }
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AgedPayablesDetailsRow, string>>)(r => r.SortKey);
}
