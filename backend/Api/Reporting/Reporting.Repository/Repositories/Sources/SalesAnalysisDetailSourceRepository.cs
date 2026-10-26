using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class SalesAnalysisDetailRow
{
    public long LineKey { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// The Sales Analysis Detail Report for export, saved views and scheduled emails (TK-229). One row for
/// each invoice or credit note line sold in the period, from the lines the Sales Analysis adds up
/// (<see cref="SalesAnalysisReportRepository.Lines"/>), credit notes included as negatives: the unit
/// price and the line total (its net sales, before tax) in the branch's base currency. A line with no
/// item has no item code and is named for that. <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class SalesAnalysisDetailSourceRepository : ReportSource<SalesAnalysisDetailRow>
{
    public override string ReportKey => "sales-analysis-detail";

    public override string Title => "Sales Analysis Detail";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SalesAnalysisDetailRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<SalesAnalysisDetailRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<SalesAnalysisDetailRow, string>("itemCode", ColumnDataType.Text, r => r.ItemCode, groupable: true),
        ReportColumn.Of<SalesAnalysisDetailRow, string>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<SalesAnalysisDetailRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SalesAnalysisDetailRow, decimal>("unitPrice", ColumnDataType.Money, r => r.UnitPrice),
        ReportColumn.Of<SalesAnalysisDetailRow, decimal>("lineTotal", ColumnDataType.Money, r => r.LineTotal, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<SalesAnalysisDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new SalesAnalysisReportRequest { FromDate = start, ToDate = end, IncludeCreditNotes = true };

        return from l in SalesAnalysisReportRepository.Lines(
                   db.Invoices, db.InvoiceDetails, db.CreditNotes, db.CreditNoteDetails, db.Items, db.StockMovements, request, start, end)
               join item in db.Items on l.ItemId equals item.ItemId into gi
               from item in gi.DefaultIfEmpty()
               select new SalesAnalysisDetailRow
               {
                   LineKey = l.LineKey,
                   DocumentDate = l.DocumentDate,
                   DocumentNo = l.DocumentNo,
                   ItemCode = item == null ? string.Empty : item.ItemCode,
                   ItemName = item == null ? "(No item)" : item.ItemName,
                   Quantity = l.Quantity,
                   UnitPrice = l.UnitPrice,
                   LineTotal = l.Net,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SalesAnalysisDetailRow, long>>)(r => r.LineKey);
}
