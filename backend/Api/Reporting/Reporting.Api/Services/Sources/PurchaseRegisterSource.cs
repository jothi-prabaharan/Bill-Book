using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class PurchaseRegisterRow
{
    public long BillId { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public string VendorName { get; set; } = null!;
    public string? VendorGstin { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class PurchaseRegisterSource : ReportSource<PurchaseRegisterRow>
{
    public override string ReportKey => "purchase-register";

    public override string Title => "Purchase Register";

    public override ReportModule Module => ReportModule.Purchase;

    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<PurchaseRegisterRow, string>("transactionType", ColumnDataType.Text, r => r.TransactionTypeCode, groupable: true),
        ReportColumn.Of<PurchaseRegisterRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<PurchaseRegisterRow, DateOnly>("documentDate", ColumnDataType.Date, r => r.DocumentDate),
        ReportColumn.Of<PurchaseRegisterRow, string>("vendorName", ColumnDataType.Text, r => r.VendorName, groupable: true),
        ReportColumn.Of<PurchaseRegisterRow, string?>("vendorGstin", ColumnDataType.Text, r => r.VendorGstin),
        ReportColumn.Of<PurchaseRegisterRow, decimal>("totalAmount", ColumnDataType.Money, r => r.TotalAmount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<PurchaseRegisterRow, long>("billId", ColumnDataType.Number, r => r.BillId, filterable: false),
    ];

    protected override IQueryable<PurchaseRegisterRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from b in db.Bills
               join c in db.Contacts on b.ContactId equals c.ContactId into gc
               from c in gc.DefaultIfEmpty()
               select new PurchaseRegisterRow
               {
                   BillId = b.BillId,
                   TransactionTypeCode = b.TransactionTypeCode,
                   DocumentNo = b.DocumentNo,
                   DocumentDate = b.DocumentDate,
                   VendorName = c != null ? c.DisplayName : "Unknown",
                   VendorGstin = c != null ? c.Gstin : null,
                   TotalAmount = b.TotalAmount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<PurchaseRegisterRow, DateOnly>>)(r => r.DocumentDate);
}
