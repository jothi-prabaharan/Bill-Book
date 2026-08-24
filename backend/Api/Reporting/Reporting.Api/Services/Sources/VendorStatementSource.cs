using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class VendorStatementRow
{
    public long ContactId { get; set; }
    public string ContactName { get; set; } = null!;
    public DateOnly LedgerDate { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public string? TransactionDesc { get; set; }
    public decimal DebitAmountBase { get; set; }
    public decimal CreditAmountBase { get; set; }
    public decimal Balance { get; set; }
}

public sealed class VendorStatementSource : ReportSource<VendorStatementRow>
{
    public override string ReportKey => "vendor-statement";

    public override string Title => "Vendor Statement";

    public override ReportModule Module => ReportModule.Purchase;

    public override string RequiredPermission => "purchase.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<VendorStatementRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<VendorStatementRow, DateOnly>("ledgerDate", ColumnDataType.Date, r => r.LedgerDate),
        ReportColumn.Of<VendorStatementRow, string>("transactionTypeCode", ColumnDataType.Text, r => r.TransactionTypeCode),
        ReportColumn.Of<VendorStatementRow, string?>("transactionDesc", ColumnDataType.Text, r => r.TransactionDesc),
        ReportColumn.Of<VendorStatementRow, decimal>("debitAmountBase", ColumnDataType.Money, r => r.DebitAmountBase, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<VendorStatementRow, decimal>("creditAmountBase", ColumnDataType.Money, r => r.CreditAmountBase, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<VendorStatementRow, decimal>("balance", ColumnDataType.Money, r => r.Balance),
        ReportColumn.Of<VendorStatementRow, long>("contactId", ColumnDataType.Number, r => r.ContactId, filterable: false),
    ];

    protected override IQueryable<VendorStatementRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from l in db.Ledger
               where l.ContactId != null
               join a in db.Accounts on l.AccountId equals a.AccountId
               where a.AccountSystemName == "Accounts Payable"
               join c in db.Contacts on l.ContactId equals c.ContactId
               where c.IsVendor
               select new VendorStatementRow
               {
                   ContactId = c.ContactId,
                   ContactName = c.DisplayName,
                   LedgerDate = l.LedgerDate,
                   TransactionTypeCode = l.TransactionTypeCode,
                   TransactionDesc = l.TransactionDesc,
                   DebitAmountBase = l.DebitAmountBase,
                   CreditAmountBase = l.CreditAmountBase,
                   Balance = l.CreditAmountBase - l.DebitAmountBase
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<VendorStatementRow, DateOnly>>)(r => r.LedgerDate);
}
