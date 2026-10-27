using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class BillsTrackRow
{
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

/// <summary>
/// The Bills Track Report for export, saved views and scheduled emails (TK-237). One row for each
/// bill dated in the period but a voided one, from the builders the page uses
/// (<see cref="BillsTrackReportRepository.Entries"/>): where it stands, its total in base currency and
/// what is still owed to the vendor on the <c>to</c> date. A draft is listed with nothing owed.
/// <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class BillsTrackSourceRepository : ReportSource<BillsTrackRow>
{
    public override string ReportKey => "bills-track";
    public override string Title => "Bills Track";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BillsTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<BillsTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<BillsTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<BillsTrackRow, string>("status", ColumnDataType.Enum, r => r.Status, groupable: true),
        ReportColumn.Of<BillsTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, AggregateFunction.Sum),
        ReportColumn.Of<BillsTrackRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, AggregateFunction.Sum),
        ReportColumn.Of<BillsTrackRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false),
    ];

    protected override IQueryable<BillsTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new BillsTrackReportRequest { FromDate = start, ToDate = end };
        IQueryable<BillTrackEntry> entries = BillsTrackReportRepository.WithStatus(
            BillsTrackReportRepository.Entries(db.Bills, db.TransactionRatios, request, start, end), null);

        return from e in entries
               join c in db.Contacts on e.ContactId equals c.ContactId
               select new BillsTrackRow
               {
                   BillId = e.DocumentId,
                   DocumentNo = e.DocumentNo,
                   DocumentDate = e.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = e.Status.ToString(),
                   TotalAmount = e.Total,
                   AmountDue = e.Due,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BillsTrackRow, long>>)(r => r.BillId);
}
