using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Shared.Kernel.Documents;

namespace Reporting.Repository.Repositories;

public class PayableInvoiceDetailRow
{
    public long BillDetailId { get; set; }
    public long BillId { get; set; }
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// The Payable Invoice Detail for export, saved views and scheduled emails (TK-236). One row for each
/// line of each posted bill dated in the period, the lines the page lists. <b>Both dates are required</b>
/// (D-67). The unit price is in the bill's own currency and the line total in base currency, so the
/// column can be added up; they are the same on a branch that is billed in its own currency. A voided
/// bill and a draft are not listed.
/// </summary>
public sealed class PayableInvoiceDetailSourceRepository : ReportSource<PayableInvoiceDetailRow>
{
    public override string ReportKey => "payable-invoice-detail";
    public override string Title => "Payable Invoice Detail";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PayableInvoiceDetailRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo, groupable: true),
        ReportColumn.Of<PayableInvoiceDetailRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PayableInvoiceDetailRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<PayableInvoiceDetailRow, string?>("itemCode", ColumnDataType.Text, r => r.ItemCode),
        ReportColumn.Of<PayableInvoiceDetailRow, string?>("itemName", ColumnDataType.Text, r => r.ItemName),
        ReportColumn.Of<PayableInvoiceDetailRow, decimal>("quantity", ColumnDataType.Quantity, r => r.Quantity, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayableInvoiceDetailRow, decimal>("unitPrice", ColumnDataType.Money, r => r.UnitPrice),
        ReportColumn.Of<PayableInvoiceDetailRow, decimal>("lineTotal", ColumnDataType.Money, r => r.LineTotal, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PayableInvoiceDetailRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false)
    ];

    protected override IQueryable<PayableInvoiceDetailRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        return from b in db.Bills
               where b.Status == DocumentStatus.Posted && b.DocumentDate >= start && b.DocumentDate <= end
               join bd in db.BillDetails on b.BillId equals bd.BillId
               join c in db.Contacts on b.ContactId equals c.ContactId
               join i in db.Items on bd.ItemId equals (long?)i.ItemId into gi
               from i in gi.DefaultIfEmpty()
               select new PayableInvoiceDetailRow
               {
                   BillDetailId = bd.BillDetailId,
                   BillId = b.BillId,
                   DocumentNo = b.DocumentNo,
                   DocumentDate = b.DocumentDate,
                   ContactName = c.DisplayName,
                   ItemCode = i != null ? i.ItemCode : null,
                   ItemName = i != null ? i.ItemName : bd.Description, // Fallback to description if no item
                   Quantity = bd.Quantity,
                   UnitPrice = bd.UnitPrice,
                   LineTotal = Math.Round(bd.LineTotal * b.ExchangeRate, 2),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PayableInvoiceDetailRow, long>>)(r => r.BillDetailId);
}
