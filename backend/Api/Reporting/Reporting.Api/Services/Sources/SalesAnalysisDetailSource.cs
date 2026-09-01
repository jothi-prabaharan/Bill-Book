using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class SalesAnalysisDetailRow
{
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SalesAnalysisDetailSource : ReportSource<SalesAnalysisDetailRow>
{
    public override string ReportKey => "sales-analysis-detail";

    public override string Title => "Sales Analysis Detail";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalesAnalysisDetailRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<SalesAnalysisDetailRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<SalesAnalysisDetailRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<SalesAnalysisDetailRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<SalesAnalysisDetailRow, decimal>("quantity", ColumnDataType.Number, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesAnalysisDetailRow, decimal>("unitPrice", ColumnDataType.Money, r => r.UnitPrice),
        ReportColumn.Of<SalesAnalysisDetailRow, decimal>("lineTotal", ColumnDataType.Money, r => r.LineTotal, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<SalesAnalysisDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from d in db.InvoiceDetails
               join i in db.Invoices on d.InvoiceId equals i.InvoiceId
               join item in db.Items on d.ItemId equals item.ItemId
               select new SalesAnalysisDetailRow
               {
                   DocumentDate = i.DocumentDate,
                   DocumentNo = i.DocumentNo,
                   ItemCode = item.ItemCode,
                   ItemName = item.ItemName,
                   Quantity = d.Quantity,
                   UnitPrice = d.UnitPrice,
                   LineTotal = d.LineTotal
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalesAnalysisDetailRow, DateOnly>>)(r => r.DocumentDate);
}
