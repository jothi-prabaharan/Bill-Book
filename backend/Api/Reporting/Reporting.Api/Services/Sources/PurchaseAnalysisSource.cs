using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class PurchaseAnalysisRow
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    public decimal Quantity { get; set; }
    public decimal GrossAmount { get; set; }
}

public sealed class PurchaseAnalysisSource : ReportSource<PurchaseAnalysisRow>
{
    public override string ReportKey => "purchase-analysis";

    public override string Title => "Purchase Analysis";

    public override ReportModule Module => ReportModule.Purchase;

    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PurchaseAnalysisRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<PurchaseAnalysisRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<PurchaseAnalysisRow, string?>("category", ColumnDataType.Text, r => r.Category, groupable: true),
        ReportColumn.Of<PurchaseAnalysisRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, AggregateFunction.Sum),
        ReportColumn.Of<PurchaseAnalysisRow, decimal>("grossAmount", ColumnDataType.Money, r => r.GrossAmount, AggregateFunction.Sum),
        ReportColumn.Of<PurchaseAnalysisRow, long>("itemId", ColumnDataType.Number, r => r.ItemId, filterable: false),
    ];

    protected override IQueryable<PurchaseAnalysisRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from bd in db.BillDetails
               join b in db.Bills on bd.BillId equals b.BillId
               where bd.ItemId != null && 
                     (start == null || b.DocumentDate >= start) &&
                     (end == null || b.DocumentDate <= end)
               join i in db.Items on bd.ItemId equals i.ItemId
               join c in db.ItemCategories on i.ItemCategoryId equals c.ItemCategoryId into categories
               from c in categories.DefaultIfEmpty()
               group bd by new { bd.ItemId, i.ItemCode, i.ItemName, c.CategoryName } into g
               select new PurchaseAnalysisRow
               {
                   ItemId = g.Key.ItemId ?? 0,
                   ItemCode = g.Key.ItemCode,
                   ItemName = g.Key.ItemName,
                   Category = g.Key.CategoryName,
                   Quantity = g.Sum(x => x.Quantity),
                   GrossAmount = g.Sum(x => x.GrossAmount)
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PurchaseAnalysisRow, string>>)(r => r.ItemName);
}
