using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// <b>Account Movement — and the file every other report is copied from.</b>
///
/// Read this before writing a report, and follow its shape rather than inventing
/// one: a row type, a column list, a query that executes nothing, and a default
/// order. Nothing about filtering, sorting, grouping, paging, totalling or
/// exporting appears here, because the engine does all of that to every report
/// alike. §9.5 is the step-by-step; this is the worked example it points at.
///
/// The report itself is the plain movement listing — every posting against every
/// account, in date order.
/// </summary>
public sealed class AccountMovementSource : ReportSource<AccountMovementRow>
{
    public override string ReportKey => "account-movement";

    public override string Title => "Account Movement";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new()
        {
            Name = "from",
            Label = "From",
            DataType = ColumnDataType.Date,
        },
        new()
        {
            Name = "to",
            Label = "To",
            DataType = ColumnDataType.Date,
        },
    ];

    /// <summary>
    /// Note what carries an aggregate and what does not. Debit and credit total;
    /// a date, a code or a reference does not, and declaring <c>Sum</c> on one of
    /// those produces a footer figure that looks like an answer.
    ///
    /// <c>account</c> is groupable and is text — grouping runs in SQL on a
    /// concatenated key, so a groupable column must be text or the column
    /// declaration throws.
    /// </summary>
    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<AccountMovementRow, DateOnly>(
            "date", ColumnDataType.Date, r => r.Date),

        ReportColumn.Of<AccountMovementRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),

        ReportColumn.Of<AccountMovementRow, string>(
            "account", ColumnDataType.Text, r => r.Account, groupable: true),

        ReportColumn.Of<AccountMovementRow, decimal>(
            "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),

        ReportColumn.Of<AccountMovementRow, decimal>(
            "credit", ColumnDataType.Money, r => r.Credit, AggregateFunction.Sum),

        ReportColumn.Of<AccountMovementRow, string?>(
            "description", ColumnDataType.Text, r => r.Description),

        ReportColumn.Of<AccountMovementRow, string?>(
            "reference", ColumnDataType.Text, r => r.Reference),

        ReportColumn.Of<AccountMovementRow, string>(
            "source", ColumnDataType.Text, r => r.Source, groupable: true),

        ReportColumn.Of<AccountMovementRow, long>(
            "accountId", ColumnDataType.Number, r => r.AccountId),
    ];

    /// <summary>
    /// The query, executing nothing.
    ///
    /// <b>Date filtering happens here rather than as a column filter</b>, because
    /// the range is a parameter: a movement report filtered to April is April's
    /// rows hidden from a report of everything, while a movement report *for*
    /// April is a different report. The opening figure of any later report depends
    /// on which of those it is.
    ///
    /// Amounts are the base-currency ones. A report that mixed a rupee row and a
    /// dollar row into one total would foot to a number in no currency at all.
    /// </summary>
    protected override IQueryable<AccountMovementRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        IQueryable<Repository.ReadModels.JournalLedgerRead> ledger = db.Ledger;

        if (parameters.Date("from") is DateOnly from)
        {
            ledger = ledger.Where(l => l.LedgerDate >= from);
        }

        if (parameters.Date("to") is DateOnly to)
        {
            ledger = ledger.Where(l => l.LedgerDate <= to);
        }

        return from l in ledger
               join a in db.Accounts on l.AccountId equals a.AccountId
               select new AccountMovementRow
               {
                   LedgerId = l.LedgerId,
                   Date = l.LedgerDate,
                   AccountId = a.AccountId,
                   AccountCode = a.AccountCode,
                   Account = a.AccountName,
                   Debit = l.DebitAmountBase,
                   Credit = l.CreditAmountBase,
                   Description = l.TransactionDesc,
                   Reference = l.TransactionTypeCode + "-" + l.TransactionId,
                   Source = l.TransactionTypeCode,
               };
    }

    /// <summary>
    /// The tie-break, and it must be unique. Two rows equal on every sort key may
    /// otherwise swap between one page and the next, which reads as a row going
    /// missing — a bug that only shows up under paging and never in a test with
    /// four rows.
    /// </summary>
    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<AccountMovementRow, long>>)(r => r.LedgerId);
}

/// <summary>
/// One row of Account Movement.
///
/// A row type per report, holding exactly what its columns read. Not an entity
/// and not shared: the projection is what EF turns into the <c>SELECT</c> list, so
/// a shared row type would fetch columns no report on it needed.
/// </summary>
public sealed class AccountMovementRow
{
    public long LedgerId { get; set; }

    public DateOnly Date { get; set; }

    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string Account { get; set; } = null!;

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public string? Description { get; set; }

    public string? Reference { get; set; }

    public string Source { get; set; } = null!;
}
