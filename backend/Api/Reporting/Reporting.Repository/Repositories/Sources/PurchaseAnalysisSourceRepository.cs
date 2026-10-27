using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class PurchaseAnalysisRow
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    public decimal Quantity { get; set; }
    public decimal GrossAmount { get; set; }
}

/// <summary>
/// The Purchase Analysis for export, saved views and scheduled emails (TK-239). One row for each item
/// bought in the period, from the lines the page adds up
/// (<see cref="PurchaseAnalysisReportRepository.Lines"/>): the units bought and the gross value in the
/// branch's base currency, less posted debit notes. A line with no item is left out here, since a row
/// needs an item to name, and so is a fixed asset bought on a bill. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class PurchaseAnalysisSourceRepository : ReportSource<PurchaseAnalysisRow>
{
    public override string ReportKey => "purchase-analysis";

    public override string Title => "Purchase Analysis";

    public override ReportModule Module => ReportModule.Purchase;

    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
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
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new PurchaseAnalysisReportRequest { FromDate = start, ToDate = end, IncludeDebitNotes = true };
        IQueryable<PurchaseGroup> groups = PurchaseAnalysisReportRepository.Groups(
            PurchaseAnalysisReportRepository.Lines(db.Bills, db.BillDetails, db.DebitNotes, db.DebitNoteDetails, db.Items, request, start, end),
            PurchaseAnalysisGroupBy.Item);

        return from g in groups
               join item in db.Items on g.Key equals item.ItemId
               join cat in db.ItemCategories on item.ItemCategoryId equals (long?)cat.ItemCategoryId into gCat
               from cat in gCat.DefaultIfEmpty()
               select new PurchaseAnalysisRow
               {
                   ItemId = item.ItemId,
                   ItemCode = item.ItemCode,
                   ItemName = item.ItemName,
                   Category = cat == null ? null : cat.CategoryName,
                   Quantity = g.Quantity,
                   GrossAmount = g.Gross,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PurchaseAnalysisRow, long>>)(r => r.ItemId);
}
