using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class AgedReceivablesDetailRow
{
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

public sealed class AgedReceivablesDetailSource : ReportSource<AgedReceivablesDetailRow>
{
    public override string ReportKey => "ar-aging-detail";

    public override string Title => "Aged Receivables Detail";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AgedReceivablesDetailRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<AgedReceivablesDetailRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<AgedReceivablesDetailRow, DateOnly?>("dueDate", ColumnDataType.Date, r => r.DueDate),
        ReportColumn.Of<AgedReceivablesDetailRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<AgedReceivablesDetailRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<AgedReceivablesDetailRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<AgedReceivablesDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from i in db.Invoices
               join c in db.Contacts on i.ContactId equals c.ContactId
               select new AgedReceivablesDetailRow
               {
                   DocumentNo = i.DocumentNo,
                   DocumentDate = i.DocumentDate,
                   DueDate = i.DueDate,
                   ContactName = c.DisplayName,
                   TotalAmount = i.TotalAmount,
                   AmountDue = i.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AgedReceivablesDetailRow, DateOnly>>)(r => r.DocumentDate);
}
