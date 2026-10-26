using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class ArAgingSummaryRow
{
    public long ContactId { get; set; }
    public string ContactCode { get; set; } = null!;
    public string ContactName { get; set; } = null!;
    public decimal Current { get; set; }
    public decimal Days1_30 { get; set; }
    public decimal Days31_60 { get; set; }
    public decimal Days61_90 { get; set; }
    public decimal Days90Plus { get; set; }
    public decimal Total { get; set; }
}

public sealed class ArAgingSummarySourceRepository : ReportSource<ArAgingSummaryRow>
{
    public override string ReportKey => "ar-aging-summary";

    public override string Title => "Aged Receivables Summary";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "asOf", Label = "As At", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<ArAgingSummaryRow, string>("contactCode", ColumnDataType.Text, r => r.ContactCode),
        ReportColumn.Of<ArAgingSummaryRow, string>("contactName", ColumnDataType.Text, r => r.ContactName),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("current", ColumnDataType.Money, r => r.Current, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days1_30", ColumnDataType.Money, r => r.Days1_30, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days31_60", ColumnDataType.Money, r => r.Days31_60, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days61_90", ColumnDataType.Money, r => r.Days61_90, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("days90Plus", ColumnDataType.Money, r => r.Days90Plus, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, decimal>("total", ColumnDataType.Money, r => r.Total, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<ArAgingSummaryRow, long>("contactId", ColumnDataType.Number, r => r.ContactId, filterable: false),
    ];

    protected override IQueryable<ArAgingSummaryRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly asOf = parameters.Date("asOf") ?? throw new ReportQueryException("As at date is required.");
        int asOfDay = asOf.DayNumber;

        // The open documents are the page's: the same documents, settlements and dates, so the
        // totals agree. Aged by due date in 30-day columns, which is what the seeded
        // columns are named for; the page offers the other widths and the invoice date.
        IQueryable<ReceivableDocument> documents = AgedReceivablesSummaryReportRepository.Documents(db.Ledger, db.Accounts, asOf);
        IQueryable<OpenReceivable> open = AgedReceivablesSummaryReportRepository.OpenDocuments(
            documents,
            AgedReceivablesSummaryReportRepository.Settlements(db.TransactionRatios, documents, asOf),
            db.Invoices);

        var perContact = from o in open
                         group o by o.ContactId into g
                         select new
                         {
                             ContactId = g.Key,
                             Current = g.Sum(o => o.Amount < 0m || asOfDay - o.DueDate.DayNumber <= 0 ? o.Amount : 0m),
                             Days1_30 = g.Sum(o => o.Amount > 0m && asOfDay - o.DueDate.DayNumber > 0 && asOfDay - o.DueDate.DayNumber <= 30 ? o.Amount : 0m),
                             Days31_60 = g.Sum(o => o.Amount > 0m && asOfDay - o.DueDate.DayNumber > 30 && asOfDay - o.DueDate.DayNumber <= 60 ? o.Amount : 0m),
                             Days61_90 = g.Sum(o => o.Amount > 0m && asOfDay - o.DueDate.DayNumber > 60 && asOfDay - o.DueDate.DayNumber <= 90 ? o.Amount : 0m),
                             Days90Plus = g.Sum(o => o.Amount > 0m && asOfDay - o.DueDate.DayNumber > 90 ? o.Amount : 0m),
                             Total = g.Sum(o => o.Amount),
                         };

        IQueryable<ArAgingSummaryRow> customers =
            from p in perContact
            join c in db.Contacts on p.ContactId equals (long?)c.ContactId
            select new ArAgingSummaryRow
            {
                ContactId = c.ContactId,
                ContactCode = c.ContactCode,
                ContactName = c.DisplayName,
                Current = p.Current,
                Days1_30 = p.Days1_30,
                Days31_60 = p.Days31_60,
                Days61_90 = p.Days61_90,
                Days90Plus = p.Days90Plus,
                Total = p.Total,
            };

        // What no customer claims, as the page lists it, so the rows add up to the receivable account.
        IQueryable<ArAgingSummaryRow> unassigned = perContact
            .Where(p => p.ContactId == null)
            .Select(p => new ArAgingSummaryRow
            {
                ContactId = 0,
                ContactCode = "",
                ContactName = AgedReceivablesSummaryReportRepository.UnassignedName,
                Current = p.Current,
                Days1_30 = p.Days1_30,
                Days31_60 = p.Days31_60,
                Days61_90 = p.Days61_90,
                Days90Plus = p.Days90Plus,
                Total = p.Total,
            });

        return customers.Concat(unassigned);
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<ArAgingSummaryRow, string>>)(r => r.ContactName);
}
