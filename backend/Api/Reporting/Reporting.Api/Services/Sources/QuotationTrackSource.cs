using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class QuotationTrackRow
{
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class QuotationTrackSource : ReportSource<QuotationTrackRow>
{
    public override string ReportKey => "quote-track";

    public override string Title => "Quotation Track";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<QuotationTrackRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<QuotationTrackRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<QuotationTrackRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<QuotationTrackRow, string>("status", ColumnDataType.Text, r => r.Status.ToString()),
        ReportColumn.Of<QuotationTrackRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<QuotationTrackRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from q in db.Quotes
               join c in db.Contacts on q.ContactId equals c.ContactId
               select new QuotationTrackRow
               {
                   DocumentNo = q.DocumentNo,
                   DocumentDate = q.DocumentDate,
                   ContactName = c.DisplayName,
                   Status = q.Status,
                   TotalAmount = q.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<QuotationTrackRow, DateOnly>>)(r => r.DocumentDate);
}
