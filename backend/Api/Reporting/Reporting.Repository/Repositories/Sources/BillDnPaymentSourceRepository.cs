using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

public class BillDnPaymentRow
{
    public long TransactionRatioId { get; set; }
    public long SpendMoneyId { get; set; }
    public string SpendMoneyNo { get; set; } = null!;
    public DateOnly PaymentDate { get; set; }
    public string ContactName { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public decimal Amount { get; set; }
}

/// <summary>
/// The Bill/DN Payment Report for export, saved views and scheduled emails (TK-238). One row for
/// each bill a posted payment dated in the period settled, read from the settlements that name the
/// payment as their target, as the page reads them; the amount is what was settled, in the branch's
/// base currency. A TDS challan, which pays the government, is not a vendor payment and is not listed.
/// <b>Both dates are required</b> (D-67).
/// </summary>
public sealed class BillDnPaymentSourceRepository : ReportSource<BillDnPaymentRow>
{
    public override string ReportKey => "bill-dn-payment";
    public override string Title => "Bill / DN Payment";
    public override ReportModule Module => ReportModule.Purchase;
    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

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
        DateOnly start = parameters.Date("from") ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to") ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        var request = new Reporting.Entity.Models.BillDnPaymentReportRequest { FromDate = start, ToDate = end };

        return from t in db.TransactionRatios
               join sm in BillDnPaymentReportRepository.Payments(db.SpendMoney, request, start, end)
                   on t.TargetTransactionId equals sm.SpendMoneyId
               join c in db.Contacts on sm.ContactId equals c.ContactId
               join b in db.Bills on t.SourceTransactionId equals b.BillId
               where !t.IsVoided && t.TargetTransactionTypeCode == "SPM" && t.SourceTransactionTypeCode == "BIL"
               select new BillDnPaymentRow
               {
                   TransactionRatioId = t.TransactionRatioId,
                   SpendMoneyId = sm.SpendMoneyId,
                   SpendMoneyNo = sm.TransactionNo ?? string.Empty,
                   PaymentDate = sm.TransactionDate,
                   ContactName = c.DisplayName,
                   DocumentNo = b.DocumentNo,
                   Amount = sm.ExchangeRate > 0m && sm.ExchangeRate != 1m ? Math.Round(t.Amount / sm.ExchangeRate, 2) : t.Amount,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BillDnPaymentRow, long>>)(r => r.TransactionRatioId);
}
