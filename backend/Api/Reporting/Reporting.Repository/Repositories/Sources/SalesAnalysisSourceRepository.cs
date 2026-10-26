using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class SalesAnalysisRow
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? CategoryName { get; set; }
    public decimal Quantity { get; set; }
    public decimal GrossAmount { get; set; }
}

/// <summary>
/// The Sales Analysis for export, saved views and scheduled emails (TK-228). One row for each item
/// sold in the period, from the lines the page adds up
/// (<see cref="SalesAnalysisReportRepository.Lines"/>): the units sold and the gross value in the
/// branch's base currency, less posted credit notes. A line with no item is left out here, since a
/// row needs an item to name. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class SalesAnalysisSourceRepository : ReportSource<SalesAnalysisRow>
{
    public override string ReportKey => "sales-analysis";

    public override string Title => "Sales Analysis";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

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
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new SalesAnalysisReportRequest { FromDate = start, ToDate = end, IncludeCreditNotes = true };
        IQueryable<AnalysisGroup> groups = SalesAnalysisReportRepository.Groups(
            SalesAnalysisReportRepository.Lines(db.Invoices, db.InvoiceDetails, db.CreditNotes, db.CreditNoteDetails, db.Items, db.StockMovements, request, start, end),
            SalesAnalysisGroupBy.Item);

        return from g in groups
               join item in db.Items on g.Key equals item.ItemId
               join cat in db.ItemCategories on item.ItemCategoryId equals (long?)cat.ItemCategoryId into gCat
               from cat in gCat.DefaultIfEmpty()
               select new SalesAnalysisRow
               {
                   ItemId = item.ItemId,
                   ItemCode = item.ItemCode,
                   ItemName = item.ItemName,
                   CategoryName = cat == null ? null : cat.CategoryName,
                   Quantity = g.Quantity,
                   GrossAmount = g.Gross,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalesAnalysisRow, long>>)(r => r.ItemId);
}
