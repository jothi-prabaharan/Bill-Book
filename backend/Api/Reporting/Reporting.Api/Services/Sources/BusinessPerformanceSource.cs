using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// The branch's key ratios over a period, one row per ratio: margins, return on
/// investment, how long customers and suppliers take, liquidity and cash.
/// Decided as D-15 (2026-09-24): Xero-style KPI ratios.
///
/// <b>Every figure is read from the ledger</b>, in base currency, through the same
/// account types the Profit &amp; Loss and Balance Sheet use, so a ratio can be
/// checked against those two reports. Each row carries its numerator and
/// denominator for exactly that reason.
///
/// <b>How accounts are classified, since the chart of accounts records less than
/// the ratios need:</b>
/// <list type="bullet">
/// <item><b>Revenue</b> is Income accounts marked for sales (<c>IsSales</c>), so
/// Sales Returns nets off and FX gains do not count as trading.
/// <b>Cost of sales</b> is Cost of Goods Sold less Purchase Returns. Net profit is
/// every Income account less every Expense account.</item>
/// <item><b>Term assets</b> are the Fixed Asset account and every account a
/// fixed-asset category names (asset and accumulated depreciation, so they are
/// net of depreciation). Every other asset is current.</item>
/// <item><b>Every liability is current</b>, because no account can be marked
/// long-term. So <i>Term assets to liabilities</i> divides by total liabilities.
/// It becomes term liabilities the day an account can say it is one.</item>
/// <item><b>Credit sales</b> are debits to Accounts Receivable in the period, and
/// <b>credit purchases</b> credits to Accounts Payable. Both include GST, as the
/// balances they are compared with do.</item>
/// </list>
///
/// A ratio whose denominator is zero, or not positive for return on investment,
/// has no value rather than a meaningless one.
/// </summary>
public sealed class BusinessPerformanceSource : ReportSource<BusinessPerformanceRow>
{
    // mst.AccountTypes ids, contractual; the same numbers ChartOfAccountsSeed uses.
    private const int Asset = 1;
    private const int Liability = 2;
    private const int Income = 4;
    private const int Expense = 5;

    // AccountSystemName of the control accounts the seed writes, as
    // Accounting.Entity.Enums.SystemAccountNames spells them. An org may rename
    // an account; it cannot rename these.
    private const string AccountsReceivable = "Accounts Receivable";
    private const string AccountsPayable = "Accounts Payable";
    private const string CostOfGoodsSold = "Cost of Goods Sold";
    private const string PurchaseReturns = "Purchase Returns";
    private const string FixedAsset = "Fixed Asset";

    public override string ReportKey => "business-performance";

    public override string Title => "Business Performance";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    /// <summary>The ratios are defined in one order; re-sorting would scatter the set.</summary>
    public override bool ForcesSortOrder => true;

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BusinessPerformanceRow, string>(
            "metric", ColumnDataType.Text, r => r.Metric),
        ReportColumn.Of<BusinessPerformanceRow, decimal?>(
            "value", ColumnDataType.Number, r => r.Value),
        ReportColumn.Of<BusinessPerformanceRow, string>(
            "unit", ColumnDataType.Enum, r => r.Unit, groupable: true),
        ReportColumn.Of<BusinessPerformanceRow, decimal>(
            "numerator", ColumnDataType.Money, r => r.Numerator),
        ReportColumn.Of<BusinessPerformanceRow, decimal?>(
            "denominator", ColumnDataType.Money, r => r.Denominator),
        ReportColumn.Of<BusinessPerformanceRow, string>(
            "calculation", ColumnDataType.Text, r => r.Calculation),
        ReportColumn.Of<BusinessPerformanceRow, int>(
            "metricOrder", ColumnDataType.Number, r => r.MetricOrder, filterable: false),
    ];

    /// <summary>
    /// With no dates, the twelve months to today. With only an end, the twelve
    /// months to it. With only a start, from it to today.
    /// </summary>
    protected override IQueryable<BusinessPerformanceRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly end = parameters.Date("to") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly start = parameters.Date("from") ?? end.AddYears(-1).AddDays(1);

        if (start > end)
        {
            throw new ReportQueryException("The start of the period must not be after its end.");
        }

        return Rows(db.Accounts, db.Ledger, db.FixedAssetCategories, start, end);
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BusinessPerformanceRow, int>>)(r => r.MetricOrder);

    /// <summary>
    /// The eight rows, over the sets rather than the context so the arithmetic can
    /// be tested over lists.
    ///
    /// <b>One pass over the ledger.</b> Every line up to the end of the period is
    /// joined to its account and grouped into a single row of conditional sums —
    /// grouped by <c>OrgId</c>, which the query filter already holds to one value,
    /// so it is one group. Each ratio is then a projection of that row, and the
    /// eight are unioned. A branch that has never posted has no group and so no
    /// rows, which is true: there is nothing to measure.
    /// </summary>
    public static IQueryable<BusinessPerformanceRow> Rows(
        IQueryable<AccountRead> accounts,
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<FixedAssetCategoryRead> categories,
        DateOnly start,
        DateOnly end)
    {
        decimal days = end.DayNumber - start.DayNumber + 1;

        var lines =
            from l in ledger
            where l.LedgerDate <= end
            join a in accounts on l.AccountId equals a.AccountId
            select new
            {
                l.OrgId,
                InPeriod = l.LedgerDate >= start,
                Opening = l.LedgerDate < start,
                Debit = l.DebitAmountBase,
                Credit = l.CreditAmountBase,
                a.AccountTypeId,
                a.IsSales,
                a.IsBank,
                a.AccountSystemName,
                IsTerm = a.AccountSystemName == FixedAsset
                    || categories.Any(c => c.AssetAccountId == a.AccountId
                        || c.AccumulatedDepreciationAccountId == a.AccountId),
            };

        IQueryable<BusinessPerformanceFigures> figures = lines
            .GroupBy(x => x.OrgId)
            .Select(g => new BusinessPerformanceFigures
            {
                Revenue = g.Sum(x => x.InPeriod && x.AccountTypeId == Income && x.IsSales
                    ? x.Credit - x.Debit : 0m),
                CostOfSales = g.Sum(x => x.InPeriod && x.AccountTypeId == Expense
                        && (x.AccountSystemName == CostOfGoodsSold
                            || x.AccountSystemName == PurchaseReturns)
                    ? x.Debit - x.Credit : 0m),
                Income = g.Sum(x => x.InPeriod && x.AccountTypeId == Income
                    ? x.Credit - x.Debit : 0m),
                Expenses = g.Sum(x => x.InPeriod && x.AccountTypeId == Expense
                    ? x.Debit - x.Credit : 0m),
                Assets = g.Sum(x => x.AccountTypeId == Asset ? x.Debit - x.Credit : 0m),
                TermAssets = g.Sum(x => x.AccountTypeId == Asset && x.IsTerm
                    ? x.Debit - x.Credit : 0m),
                Liabilities = g.Sum(x => x.AccountTypeId == Liability
                    ? x.Credit - x.Debit : 0m),
                Cash = g.Sum(x => x.IsBank ? x.Debit - x.Credit : 0m),
                ReceivablesOpening = g.Sum(x => x.Opening && x.AccountSystemName == AccountsReceivable
                    ? x.Debit - x.Credit : 0m),
                ReceivablesClosing = g.Sum(x => x.AccountSystemName == AccountsReceivable
                    ? x.Debit - x.Credit : 0m),
                CreditSales = g.Sum(x => x.InPeriod && x.AccountSystemName == AccountsReceivable
                    ? x.Debit : 0m),
                PayablesOpening = g.Sum(x => x.Opening && x.AccountSystemName == AccountsPayable
                    ? x.Credit - x.Debit : 0m),
                PayablesClosing = g.Sum(x => x.AccountSystemName == AccountsPayable
                    ? x.Credit - x.Debit : 0m),
                CreditPurchases = g.Sum(x => x.InPeriod && x.AccountSystemName == AccountsPayable
                    ? x.Credit : 0m),
            });

        IQueryable<BusinessPerformanceRow> grossMargin = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.GrossProfitMargin,
            Metric = "Gross profit margin",
            Unit = nameof(BusinessPerformanceUnit.Percent),
            Numerator = f.Revenue - f.CostOfSales,
            Denominator = f.Revenue,
            Value = f.Revenue == 0m
                ? (decimal?)null
                : Math.Round((f.Revenue - f.CostOfSales) / f.Revenue * 100m, 2),
            Calculation = "Gross profit ÷ revenue × 100",
        });

        IQueryable<BusinessPerformanceRow> netMargin = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.NetProfitMargin,
            Metric = "Net profit margin",
            Unit = nameof(BusinessPerformanceUnit.Percent),
            Numerator = f.Income - f.Expenses,
            Denominator = f.Revenue,
            Value = f.Revenue == 0m
                ? (decimal?)null
                : Math.Round((f.Income - f.Expenses) / f.Revenue * 100m, 2),
            Calculation = "Net profit ÷ revenue × 100",
        });

        IQueryable<BusinessPerformanceRow> returnOnInvestment = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.ReturnOnInvestment,
            Metric = "Return on investment (p.a.)",
            Unit = nameof(BusinessPerformanceUnit.Percent),
            Numerator = (f.Income - f.Expenses) * 365m / days,
            Denominator = f.Assets - f.Liabilities,
            Value = f.Assets - f.Liabilities <= 0m
                ? (decimal?)null
                : Math.Round(
                    (f.Income - f.Expenses) * 365m / days / (f.Assets - f.Liabilities) * 100m, 2),
            Calculation = "Net profit for a year at this period's rate ÷ net assets at the end × 100",
        });

        IQueryable<BusinessPerformanceRow> customerDays = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.AverageDaysCustomersTakeToPay,
            Metric = "Average time customers take to pay",
            Unit = nameof(BusinessPerformanceUnit.Days),
            Numerator = (f.ReceivablesOpening + f.ReceivablesClosing) / 2m,
            Denominator = f.CreditSales,
            Value = f.CreditSales == 0m
                ? (decimal?)null
                : Math.Round((f.ReceivablesOpening + f.ReceivablesClosing) / 2m / f.CreditSales * days, 2),
            Calculation = "Average accounts receivable ÷ credit sales × days in the period",
        });

        IQueryable<BusinessPerformanceRow> supplierDays = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.AverageDaysToPaySuppliers,
            Metric = "Average time to pay suppliers",
            Unit = nameof(BusinessPerformanceUnit.Days),
            Numerator = (f.PayablesOpening + f.PayablesClosing) / 2m,
            Denominator = f.CreditPurchases,
            Value = f.CreditPurchases == 0m
                ? (decimal?)null
                : Math.Round((f.PayablesOpening + f.PayablesClosing) / 2m / f.CreditPurchases * days, 2),
            Calculation = "Average accounts payable ÷ credit purchases × days in the period",
        });

        IQueryable<BusinessPerformanceRow> currentRatio = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.CurrentAssetsToLiabilities,
            Metric = "Current assets to liabilities",
            Unit = nameof(BusinessPerformanceUnit.Times),
            Numerator = f.Assets - f.TermAssets,
            Denominator = f.Liabilities,
            Value = f.Liabilities == 0m
                ? (decimal?)null
                : Math.Round((f.Assets - f.TermAssets) / f.Liabilities, 2),
            Calculation = "Current assets ÷ current liabilities, at the end of the period",
        });

        IQueryable<BusinessPerformanceRow> termRatio = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.TermAssetsToLiabilities,
            Metric = "Term assets to liabilities",
            Unit = nameof(BusinessPerformanceUnit.Times),
            Numerator = f.TermAssets,
            Denominator = f.Liabilities,
            Value = f.Liabilities == 0m
                ? (decimal?)null
                : Math.Round(f.TermAssets / f.Liabilities, 2),
            Calculation = "Fixed assets net of depreciation ÷ total liabilities, at the end of the period",
        });

        IQueryable<BusinessPerformanceRow> cash = figures.Select(f => new BusinessPerformanceRow
        {
            MetricOrder = (int)BusinessPerformanceMetric.TotalCashBalance,
            Metric = "Total cash balance",
            Unit = nameof(BusinessPerformanceUnit.Amount),
            Numerator = f.Cash,
            Denominator = (decimal?)null,
            Value = f.Cash,
            Calculation = "Bank and cash accounts, less overdrafts and cards, at the end of the period",
        });

        return grossMargin
            .Concat(netMargin)
            .Concat(returnOnInvestment)
            .Concat(customerDays)
            .Concat(supplierDays)
            .Concat(currentRatio)
            .Concat(termRatio)
            .Concat(cash);
    }
}

/// <summary>
/// The ledger summed once, in the shapes the ratios need. Balances are as at the
/// end of the period unless named opening; flows are within it.
/// </summary>
public sealed class BusinessPerformanceFigures
{
    public decimal Revenue { get; set; }

    public decimal CostOfSales { get; set; }

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    public decimal Assets { get; set; }

    public decimal TermAssets { get; set; }

    public decimal Liabilities { get; set; }

    public decimal Cash { get; set; }

    public decimal ReceivablesOpening { get; set; }

    public decimal ReceivablesClosing { get; set; }

    public decimal CreditSales { get; set; }

    public decimal PayablesOpening { get; set; }

    public decimal PayablesClosing { get; set; }

    public decimal CreditPurchases { get; set; }
}

/// <summary>One ratio for the period.</summary>
public sealed class BusinessPerformanceRow
{
    /// <summary>The <see cref="BusinessPerformanceMetric"/> value; the report's order.</summary>
    public int MetricOrder { get; set; }

    public string Metric { get; set; } = null!;

    /// <summary>Null when the ratio's denominator makes it meaningless.</summary>
    public decimal? Value { get; set; }

    /// <summary>The <see cref="BusinessPerformanceUnit"/> name.</summary>
    public string Unit { get; set; } = null!;

    public decimal Numerator { get; set; }

    /// <summary>Null for the cash balance, which is an amount rather than a ratio.</summary>
    public decimal? Denominator { get; set; }

    public string Calculation { get; set; } = null!;
}
