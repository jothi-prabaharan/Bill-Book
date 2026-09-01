using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class AgedPayablesDetailsRow
{
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string ContactName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal AmountDue { get; set; }
}

public sealed class AgedPayablesDetailsSource : ReportSource<AgedPayablesDetailsRow>
{
    public override string ReportKey => "ap-aging-detail";
    public override string Title => "Aged Payables Details";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "asOf", Label = "As Of", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AgedPayablesDetailsRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<AgedPayablesDetailsRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<AgedPayablesDetailsRow, DateOnly>("dueDate", ColumnDataType.Date, r => r.DueDate),
        ReportColumn.Of<AgedPayablesDetailsRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<AgedPayablesDetailsRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, AggregateFunction.Sum),
        ReportColumn.Of<AgedPayablesDetailsRow, decimal>("amountDue", ColumnDataType.Money, r => r.AmountDue, AggregateFunction.Sum),
        ReportColumn.Of<AgedPayablesDetailsRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false),
    ];

    protected override IQueryable<AgedPayablesDetailsRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? asOf = parameters.Date("asOf");

        return from b in db.Bills
               where asOf == null || b.DocumentDate <= asOf
               join c in db.Contacts on b.ContactId equals c.ContactId
               select new AgedPayablesDetailsRow
               {
                   BillId = b.BillId,
                   DocumentNo = b.DocumentNo,
                   DocumentDate = b.DocumentDate,
                   DueDate = b.DueDate,
                   ContactName = c.DisplayName,
                   TotalAmount = b.TotalAmount,
                   AmountDue = b.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AgedPayablesDetailsRow, DateOnly>>)(r => r.DocumentDate);
}
