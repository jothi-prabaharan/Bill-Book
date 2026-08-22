using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// <b>Account Transaction — the fullest report in the product, and the second
/// template.</b>
///
/// Copy this one for any report carrying a running balance or another figure
/// defined over an ordering: General Ledger Summary and Bank Summary both are.
/// Copy <see cref="AccountMovementSource"/> instead for a plain listing.
///
/// Two things make it different from the plain listing, and both are in the
/// engine rather than here — which is the point of it being a template:
///
/// <list type="number">
/// <item><b>It forces its own order.</b> A running balance is only true of one
/// ordering, so allowing a sort by, say, amount would leave every balance
/// arithmetically correct for an order the reader cannot see. The grid disables
/// the other columns' sorting when this is set.</item>
/// <item><b>Its second page does not start at zero.</b> The opening figure is the
/// sum of everything the ordering puts ahead of the page, fetched as one
/// aggregate — without it, page 2 is wrong by the whole of page 1 while still
/// adding up internally, which is the dangerous kind of wrong.</item>
/// </list>
/// </summary>
public sealed class AccountTransactionSource : ReportSource<AccountTransactionRow>
{
    public override string ReportKey => "account-transaction";

    public override string Title => "Account Transaction";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    /// <summary>The running balance is why. See the class remarks.</summary>
    public override bool ForcesSortOrder => true;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
        new()
        {
            Name = "accountId",
            Label = "Account",
            DataType = ColumnDataType.Number,
        },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AccountTransactionRow, DateOnly>(
            "date", ColumnDataType.Date, r => r.Date),

        ReportColumn.Of<AccountTransactionRow, string>(
            "transactionNo", ColumnDataType.Text, r => r.TransactionNo),

        ReportColumn.Of<AccountTransactionRow, string>(
            "source", ColumnDataType.Text, r => r.Source, groupable: true),

        ReportColumn.Of<AccountTransactionRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),

        ReportColumn.Of<AccountTransactionRow, string>(
            "account", ColumnDataType.Text, r => r.Account, groupable: true),

        ReportColumn.Of<AccountTransactionRow, string?>(
            "contactCode", ColumnDataType.Text, r => r.ContactCode),

        ReportColumn.Of<AccountTransactionRow, string?>(
            "contactName", ColumnDataType.Text, r => r.ContactName),

        ReportColumn.Of<AccountTransactionRow, string?>(
            "description", ColumnDataType.Text, r => r.Description),

        ReportColumn.Of<AccountTransactionRow, string?>(
            "reference", ColumnDataType.Text, r => r.Reference),

        // The document's own currency and the rate it was booked at. Both are
        // required beside any (Source) column, or the figure means nothing.
        ReportColumn.Of<AccountTransactionRow, string>(
            "currency", ColumnDataType.Text, r => r.Currency, groupable: true),

        ReportColumn.Of<AccountTransactionRow, decimal>(
            "exchangeRate", ColumnDataType.Rate, r => r.ExchangeRate),

        ReportColumn.Of<AccountTransactionRow, decimal>(
            "debitSource", ColumnDataType.Money, r => r.DebitSource, AggregateFunction.Sum),

        ReportColumn.Of<AccountTransactionRow, decimal>(
            "creditSource", ColumnDataType.Money, r => r.CreditSource, AggregateFunction.Sum),

        ReportColumn.Of<AccountTransactionRow, decimal>(
            "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),

        ReportColumn.Of<AccountTransactionRow, decimal>(
            "credit", ColumnDataType.Money, r => r.Credit, AggregateFunction.Sum),

        // **No aggregate.** Totalling a running balance produces the sum of every
        // intermediate balance, which is a number with no meaning that looks like
        // a closing figure.
        ReportColumn.Of<AccountTransactionRow, decimal>(
            "runningBalance", ColumnDataType.Money, r => r.RunningBalance,
            sortable: false, filterable: false),

        ReportColumn.Of<AccountTransactionRow, long>(
            "ledgerId", ColumnDataType.Number, r => r.LedgerId, filterable: false),
    ];

    protected override IQueryable<AccountTransactionRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");
        long? accountId = parameters.Id("accountId");

        IQueryable<Repository.ReadModels.JournalLedgerRead> ledger = db.Ledger;

        if (start is DateOnly from)
        {
            ledger = ledger.Where(l => l.LedgerDate >= from);
        }

        if (end is DateOnly to)
        {
            ledger = ledger.Where(l => l.LedgerDate <= to);
        }

        if (accountId is long account)
        {
            ledger = ledger.Where(l => l.AccountId == account);
        }

        return from l in ledger
               join a in db.Accounts on l.AccountId equals a.AccountId
               // The contact is denormalized onto the leg, so this join is
               // optional rather than the only way to name one — a bank leg has
               // no contact and must still appear.
               join c in db.Contacts on l.ContactId equals c.ContactId into contacts
               from c in contacts.DefaultIfEmpty()
               select new AccountTransactionRow
               {
                   LedgerId = l.LedgerId,
                   Date = l.LedgerDate,
                   TransactionNo = l.TransactionTypeCode + "-" + l.TransactionId,
                   Source = l.TransactionTypeCode,
                   AccountId = a.AccountId,
                   AccountCode = a.AccountCode,
                   Account = a.AccountName,
                   ContactCode = c != null ? c.ContactCode : null,
                   ContactName = c != null ? c.DisplayName : null,
                   Description = l.TransactionDesc,
                   Reference = l.TransactionTypeCode + "-" + l.TransactionId,
                   Currency = l.CurrencyCode,
                   ExchangeRate = l.ExchangeRate,
                   DebitSource = l.DebitAmount,
                   CreditSource = l.CreditAmount,
                   Debit = l.DebitAmountBase,
                   Credit = l.CreditAmountBase,
                   RunningBalance = 0m,
               };
    }

    /// <summary>
    /// Date, then ledger id. The id breaks ties within a day <b>and</b> makes the
    /// order total, which a running balance requires: two orderings of the same
    /// day's rows give two different sets of intermediate balances, and a report
    /// that shuffles between refreshes is one nobody can reconcile against.
    /// </summary>
    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AccountTransactionRow, long>>)(r => r.LedgerId);

    protected override async Task<decimal?> OpeningBalanceAsync(
        IQueryable<AccountTransactionRow> rowsBeforeThisPage, CancellationToken ct) =>
        await rowsBeforeThisPage.SumAsync(r => r.Debit - r.Credit, ct);

    /// <summary>
    /// Walks the page, carrying the balance forward. Debits add and credits
    /// subtract, which is the convention every account in the chart is read with —
    /// a credit account simply runs negative, and its report says so rather than
    /// flipping the sign and leaving the reader to guess which way it went.
    /// </summary>
    protected override void ApplyRunningTotals(
        IReadOnlyList<AccountTransactionRow> page, decimal opening)
    {
        decimal balance = opening;

        foreach (AccountTransactionRow row in page)
        {
            balance += row.Debit - row.Credit;
            row.RunningBalance = balance;
        }
    }
}

/// <summary>One leg of one posting, with everything Account Transaction shows.</summary>
public sealed class AccountTransactionRow
{
    public long LedgerId { get; set; }

    public DateOnly Date { get; set; }

    public string TransactionNo { get; set; } = null!;

    public string Source { get; set; } = null!;

    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string Account { get; set; } = null!;

    public string? ContactCode { get; set; }

    public string? ContactName { get; set; }

    public string? Description { get; set; }

    public string? Reference { get; set; }

    public string Currency { get; set; } = null!;

    public decimal ExchangeRate { get; set; }

    /// <summary>The document's own currency — meaningless without Currency beside it.</summary>
    public decimal DebitSource { get; set; }

    public decimal CreditSource { get; set; }

    /// <summary>Base currency. What every total on this report sums.</summary>
    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    /// <summary>Filled after the page is fetched — see <c>ApplyRunningTotals</c>.</summary>
    public decimal RunningBalance { get; set; }
}
