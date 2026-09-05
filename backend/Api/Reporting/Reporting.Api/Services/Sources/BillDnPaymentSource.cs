using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

public class BillDnPaymentRow
{
    public long SpendMoneyId { get; set; }
    public string SpendMoneyNo { get; set; } = null!;
    public DateOnly PaymentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public decimal Amount { get; set; }
}

public sealed class BillDnPaymentSource : ReportSource<BillDnPaymentRow>
{
    public override string ReportKey => "bill-dn-payment";
    public override string Title => "Bill / DN Payment";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BillDnPaymentRow, string>("spendMoneyNo", ColumnDataType.Text, r => r.SpendMoneyNo, groupable: true),
        ReportColumn.Of<BillDnPaymentRow, DateOnly>("paymentDate", ColumnDataType.Date, r => r.PaymentDate),
        ReportColumn.Of<BillDnPaymentRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<BillDnPaymentRow, string>("documentNo", ColumnDataType.Text, r => r.DocumentNo),
        ReportColumn.Of<BillDnPaymentRow, decimal>("amount", ColumnDataType.Money, r => r.Amount, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BillDnPaymentRow, long>("spendMoneyId", ColumnDataType.Number, r => r.SpendMoneyId, filterable: false)
    ];

    protected override IQueryable<BillDnPaymentRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from sm in db.Set<SpendMoneyRead>()
               join smd in db.Set<SpendMoneyDetailRead>() on sm.SpendMoneyId equals smd.SpendMoneyId
               where smd.MappingTransactionTypeCode == "BIL" && smd.MappingTransactionId != null
               join b in db.Bills on smd.MappingTransactionId equals b.BillId
               join c in db.Contacts on sm.ContactId equals c.ContactId into gc
               from c in gc.DefaultIfEmpty()
               select new BillDnPaymentRow
               {
                   SpendMoneyId = sm.SpendMoneyId,
                   SpendMoneyNo = sm.TransactionNo ?? "",
                   PaymentDate = sm.TransactionDate,
                   ContactName = c != null ? c.DisplayName : "Unknown",
                   DocumentNo = b.DocumentNo,
                   Amount = smd.Amount
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BillDnPaymentRow, DateOnly>>)(r => r.PaymentDate);
}
