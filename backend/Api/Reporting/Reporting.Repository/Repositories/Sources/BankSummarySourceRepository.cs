using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Bank Summary for export, saved views and scheduled emails (TK-219). Its
/// figures are the page's, from <see cref="BankSummaryReportRepository"/>'s builders:
/// the opening ledger balance of each bank account, what it received and spent in
/// the period with any revaluation taken out, the revaluation, and the closing
/// balance. <b>The period is required</b> (D-67). The ledger is summed once for all
/// the accounts, not once for each, which is what answered 500 here.
/// </summary>
public sealed class BankSummarySourceRepository : ReportSource<BankSummaryRow>
{
    public override string ReportKey => "bank-summary";

    public override string Title => "Bank Summary";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override bool ForcesSortOrder => true;

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BankSummaryRow, string>(
            "accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),

        ReportColumn.Of<BankSummaryRow, string>(
            "accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),

        ReportColumn.Of<BankSummaryRow, string>(
            "currencyCode", ColumnDataType.Text, r => r.CurrencyCode, groupable: true),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "openingBalance", ColumnDataType.Money, r => r.OpeningBalance, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "received", ColumnDataType.Money, r => r.Received, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "spent", ColumnDataType.Money, r => r.Spent, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "closingBalance", ColumnDataType.Money, r => r.ClosingBalance, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "bankRevaluation", ColumnDataType.Money, r => r.BankRevaluation, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, long>(
            "bankAccountId", ColumnDataType.Number, r => r.BankAccountId, filterable: false),
    ];

    protected override IQueryable<BankSummaryRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        (DateOnly start, DateOnly end) = TrialBalanceSourceRepository.PeriodOf(parameters);
        IQueryable<BankFigures> figures = BankSummaryReportRepository.Figures(db.Ledger, db.Accounts, db.BankAccounts, start, end);

        // Accounts with a leg, then accounts switched on with none: a left join with a null
        // check translates for the rows and not for the totals the runner sums over it.
        IQueryable<BankSummaryRow> posted =
            from b in db.BankAccounts
            join f in figures on b.BankAccountId equals f.BankAccountId
            where b.IsActive || f.OpeningBase != 0m || f.Count > 0
            select new BankSummaryRow
            {
                BankAccountId = b.BankAccountId,
                BankAccountType = b.AccountType,
                AccountName = b.AccountName,
                CurrencyCode = b.CurrencyCode,
                OpeningBalance = f.OpeningBase,
                Received = f.ReceivedBase,
                Spent = f.SpentBase,
                BankRevaluation = f.RevaluationBase,
                ClosingBalance = f.OpeningBase + f.ReceivedBase - f.SpentBase + f.RevaluationBase,
            };

        IQueryable<BankSummaryRow> idle =
            from b in db.BankAccounts
            where b.IsActive && !figures.Any(f => f.BankAccountId == b.BankAccountId)
            select new BankSummaryRow
            {
                BankAccountId = b.BankAccountId,
                BankAccountType = b.AccountType,
                AccountName = b.AccountName,
                CurrencyCode = b.CurrencyCode,
                OpeningBalance = 0m,
                Received = 0m,
                Spent = 0m,
                BankRevaluation = 0m,
                ClosingBalance = 0m,
            };

        return posted.Concat(idle);
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BankSummaryRow, long>>)(r => r.BankAccountId);

    protected override async Task<decimal?> OpeningBalanceAsync(
        IQueryable<BankSummaryRow> rowsBeforeThisPage, CancellationToken ct) =>
        await rowsBeforeThisPage.SumAsync(r => r.OpeningBalance, ct);

    /// <summary>The kind of account is the bank account's own (savings, current, wallet, cash), not the chart's type for its ledger account.</summary>
    protected override Task FormatRowsAsync(IReadOnlyList<BankSummaryRow> page, CancellationToken ct)
    {
        foreach (BankSummaryRow row in page)
        {
            row.AccountType = row.BankAccountType.ToString();
        }

        return Task.CompletedTask;
    }
}

public sealed class BankSummaryRow
{
    public long BankAccountId { get; set; }
    public BankAccountType BankAccountType { get; set; }
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public decimal Received { get; set; }
    public decimal Spent { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal BankRevaluation { get; set; }
}
