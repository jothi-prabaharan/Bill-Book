using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public class CustomerStatementRow
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

public sealed class CustomerStatementSource : ReportSource<CustomerStatementRow>
{
    public override string ReportKey => "customer-statement";

    public override string Title => "Customer Statement";

    public override ReportModule Module => ReportModule.Sales;

    public override string RequiredPermission => "sales.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<CustomerStatementRow, string>("contactName", ColumnDataType.Text, r => r.ContactName, groupable: true),
        ReportColumn.Of<CustomerStatementRow, DateOnly>("ledgerDate", ColumnDataType.Date, r => r.LedgerDate),
        ReportColumn.Of<CustomerStatementRow, string>("transactionTypeCode", ColumnDataType.Text, r => r.TransactionTypeCode),
        ReportColumn.Of<CustomerStatementRow, string?>("transactionDesc", ColumnDataType.Text, r => r.TransactionDesc),
        ReportColumn.Of<CustomerStatementRow, decimal>("debitAmountBase", ColumnDataType.Money, r => r.DebitAmountBase, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<CustomerStatementRow, decimal>("creditAmountBase", ColumnDataType.Money, r => r.CreditAmountBase, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<CustomerStatementRow, decimal>("balance", ColumnDataType.Money, r => r.Balance),
        ReportColumn.Of<CustomerStatementRow, long>("contactId", ColumnDataType.Number, r => r.ContactId, filterable: false),
    ];

    protected override IQueryable<CustomerStatementRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        // Balance running total is usually computed in C# or via window functions. 
        // For standard LINQ to Entities without window functions, we output the raw lines 
        // and the client or reporting grid handles the running balance.
        return from l in db.Ledger
               where l.ContactId != null
               join a in db.Accounts on l.AccountId equals a.AccountId
               where a.AccountSystemName == "Accounts Receivable"
               join c in db.Contacts on l.ContactId equals c.ContactId
               where c.IsCustomer
               select new CustomerStatementRow
               {
                   ContactId = c.ContactId,
                   ContactName = c.DisplayName,
                   LedgerDate = l.LedgerDate,
                   TransactionTypeCode = l.TransactionTypeCode,
                   TransactionDesc = l.TransactionDesc,
                   DebitAmountBase = l.DebitAmountBase,
                   CreditAmountBase = l.CreditAmountBase,
                   Balance = l.DebitAmountBase - l.CreditAmountBase // Will be accumulated on client side if needed
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<CustomerStatementRow, DateOnly>>)(r => r.LedgerDate);
}
