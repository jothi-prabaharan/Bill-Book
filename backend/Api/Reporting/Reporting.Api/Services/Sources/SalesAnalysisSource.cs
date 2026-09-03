using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class SalesAnalysisRow
{
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? CategoryName { get; set; }
    public decimal Quantity { get; set; }
    public decimal GrossAmount { get; set; }
}

public sealed class SalesAnalysisSource : ReportSource<SalesAnalysisRow>
{
    public override string ReportKey => "sales-analysis";

    public override string Title => "Sales Analysis";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalesAnalysisRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode, groupable: true),
        ReportColumn.Of<SalesAnalysisRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<SalesAnalysisRow, string?>("categoryName", ColumnDataType.Text, r => r.CategoryName, groupable: true),
        ReportColumn.Of<SalesAnalysisRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesAnalysisRow, decimal>("grossAmount", ColumnDataType.Money, r => r.GrossAmount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<SalesAnalysisRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from d in db.InvoiceDetails
               join i in db.Invoices on d.InvoiceId equals i.InvoiceId
               join item in db.Items on d.ItemId equals item.ItemId
               join cat in db.ItemCategories on item.ItemCategoryId equals cat.ItemCategoryId into gCat
               from cat in gCat.DefaultIfEmpty()
               group d by new { item.ItemCode, item.ItemName, cat.CategoryName } into g
               select new SalesAnalysisRow
               {
                   ItemCode = g.Key.ItemCode,
                   ItemName = g.Key.ItemName,
                   CategoryName = g.Key.CategoryName,
                   Quantity = g.Sum(x => x.Quantity),
                   GrossAmount = g.Sum(x => x.GrossAmount)
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalesAnalysisRow, string>>)(r => r.ItemCode);
}
