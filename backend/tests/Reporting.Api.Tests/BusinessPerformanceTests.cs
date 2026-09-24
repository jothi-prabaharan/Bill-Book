using Reporting.Api.Services.Sources;
using Reporting.Entity.Enums;
using Reporting.Repository.ReadModels;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Business Performance ratios over one small, complete set of books, worked
/// out by hand in the comments beside each figure.
///
/// Proves the arithmetic and the account classification, over lists. Translation
/// to SQL is not proved here — LINQ to Objects will run anything.
/// </summary>
public sealed class BusinessPerformanceTests
{
    private static readonly DateOnly Start = new(2026, 4, 1);
    private static readonly DateOnly End = new(2027, 3, 31); // 365 days

    private const int Asset = 1;
    private const int Liability = 2;
    private const int Equity = 3;
    private const int Income = 4;
    private const int Expense = 5;

    private const long Bank = 1;
    private const long Receivable = 2;
    private const long Inventory = 3;
    private const long Plant = 4;
    private const long PlantDepreciation = 5;
    private const long FixedAssetHolding = 6;
    private const long Payable = 7;
    private const long OutputGst = 8;
    private const long Capital = 9;
    private const long Sales = 10;
    private const long SalesReturns = 11;
    private const long FxGain = 12;
    private const long CostOfGoodsSold = 13;
    private const long PurchaseReturns = 14;
    private const long Rent = 15;
    private const long DepreciationExpense = 16;

    [Fact]
    public void Gross_profit_margin_is_trading_revenue_less_cost_of_sales()
    {
        // Revenue 100,000 − 5,000 returns = 95,000. Cost of sales 60,000 − 2,000
        // purchase returns = 58,000. The FX gain is income but not revenue.
        BusinessPerformanceRow row = AYearOfTrading().Metric(BusinessPerformanceMetric.GrossProfitMargin);

        Assert.Equal(37_000m, row.Numerator);
        Assert.Equal(95_000m, row.Denominator);
        Assert.Equal(38.95m, row.Value); // 37,000 ÷ 95,000 × 100
        Assert.Equal("Percent", row.Unit);
    }

    [Fact]
    public void Net_profit_margin_takes_every_income_and_expense_account()
    {
        // Income 95,000 + 1,000 FX = 96,000. Expenses 58,000 + 12,000 rent +
        // 3,000 depreciation = 73,000.
        BusinessPerformanceRow row = AYearOfTrading().Metric(BusinessPerformanceMetric.NetProfitMargin);

        Assert.Equal(23_000m, row.Numerator);
        Assert.Equal(95_000m, row.Denominator);
        Assert.Equal(24.21m, row.Value);
    }

    [Fact]
    public void Return_on_investment_is_annual_profit_over_net_assets_at_the_end()
    {
        // Net assets 184,000 − 41,000 = 143,000; the period is exactly a year.
        BusinessPerformanceRow row = AYearOfTrading().Metric(BusinessPerformanceMetric.ReturnOnInvestment);

        Assert.Equal(23_000m, row.Numerator);
        Assert.Equal(143_000m, row.Denominator);
        Assert.Equal(16.08m, row.Value);
    }

    [Fact]
    public void Return_on_investment_scales_a_short_period_to_a_year()
    {
        Books books = new();
        books.Post(new DateOnly(2026, 4, 1), (Bank, 10_000m), (Capital, -10_000m));
        books.Post(new DateOnly(2026, 4, 10), (Bank, 1_000m), (Sales, -1_000m));

        DateOnly start = new(2026, 4, 1);
        DateOnly end = new(2026, 4, 30); // 30 days

        BusinessPerformanceRow row = books.Metric(BusinessPerformanceMetric.ReturnOnInvestment, start, end);

        Assert.Equal(1_000m * 365m / 30m, row.Numerator);
        Assert.Equal(11_000m, row.Denominator);
        Assert.Equal(Math.Round(1_000m * 365m / 30m / 11_000m * 100m, 2), row.Value);
    }

    [Fact]
    public void Customer_days_average_the_receivable_against_credit_sales()
    {
        // Receivables 20,000 at the start and 43,000 at the end; 118,000 invoiced.
        BusinessPerformanceRow row = AYearOfTrading().Metric(BusinessPerformanceMetric.AverageDaysCustomersTakeToPay);

        Assert.Equal(31_500m, row.Numerator);
        Assert.Equal(118_000m, row.Denominator);
        Assert.Equal(97.44m, row.Value); // 31,500 ÷ 118,000 × 365
        Assert.Equal("Days", row.Unit);
    }

    [Fact]
    public void Supplier_days_average_the_payable_against_credit_purchases()
    {
        // Payables 10,000 at the start and 23,000 at the end; 55,000 billed,
        // including the capital line posted to the Fixed Asset account.
        BusinessPerformanceRow row = AYearOfTrading().Metric(BusinessPerformanceMetric.AverageDaysToPaySuppliers);

        Assert.Equal(16_500m, row.Numerator);
        Assert.Equal(55_000m, row.Denominator);
        Assert.Equal(109.50m, row.Value);
    }

    [Fact]
    public void Fixed_assets_net_of_depreciation_are_term_and_everything_else_is_current()
    {
        // Term: plant 30,000 − 3,000 depreciation + 5,000 on the holding account.
        // Current: bank 109,000 + receivables 43,000 + inventory 0.
        Books books = AYearOfTrading();
        BusinessPerformanceRow current = books.Metric(BusinessPerformanceMetric.CurrentAssetsToLiabilities);
        BusinessPerformanceRow term = books.Metric(BusinessPerformanceMetric.TermAssetsToLiabilities);

        Assert.Equal(152_000m, current.Numerator);
        Assert.Equal(41_000m, current.Denominator);
        Assert.Equal(3.71m, current.Value);
        Assert.Equal("Times", current.Unit);

        Assert.Equal(32_000m, term.Numerator);
        Assert.Equal(41_000m, term.Denominator);
        Assert.Equal(0.78m, term.Value);
    }

    [Fact]
    public void Total_cash_is_the_bank_accounts_at_the_end_and_nothing_after_it()
    {
        BusinessPerformanceRow row = AYearOfTrading().Metric(BusinessPerformanceMetric.TotalCashBalance);

        Assert.Equal(109_000m, row.Value);
        Assert.Equal(109_000m, row.Numerator);
        Assert.Null(row.Denominator);
        Assert.Equal("Amount", row.Unit);
    }

    [Fact]
    public void A_ratio_with_nothing_to_divide_by_has_no_value()
    {
        Books books = new();
        books.Post(new DateOnly(2026, 5, 1), (Rent, 500m), (Bank, -500m));

        Assert.Null(books.Metric(BusinessPerformanceMetric.GrossProfitMargin).Value);
        Assert.Null(books.Metric(BusinessPerformanceMetric.NetProfitMargin).Value);
        Assert.Null(books.Metric(BusinessPerformanceMetric.AverageDaysCustomersTakeToPay).Value);
        Assert.Null(books.Metric(BusinessPerformanceMetric.AverageDaysToPaySuppliers).Value);
        Assert.Null(books.Metric(BusinessPerformanceMetric.CurrentAssetsToLiabilities).Value);

        // Net assets are −500: a return on a negative investment means nothing.
        Assert.Null(books.Metric(BusinessPerformanceMetric.ReturnOnInvestment).Value);
    }

    [Fact]
    public void Every_ratio_appears_once_in_its_defined_order()
    {
        List<BusinessPerformanceRow> rows = [.. AYearOfTrading().Rows(Start, End).OrderBy(r => r.MetricOrder)];

        Assert.Equal(
            Enum.GetValues<BusinessPerformanceMetric>().Select(m => (int)m),
            rows.Select(r => r.MetricOrder));
    }

    [Fact]
    public void Books_with_nothing_posted_have_no_ratios()
    {
        Assert.Empty(new Books().Rows(Start, End));
    }

    [Fact]
    public void The_report_holds_its_own_order_and_totals_nothing()
    {
        BusinessPerformanceSource source = new();

        // One column mixes percentages, days and amounts; a total of it is nonsense.
        Assert.True(source.ForcesSortOrder);
        Assert.DoesNotContain(source.Columns, c => c.IsAggregatable);
        Assert.Equal(ReportModule.Accounting, source.Module);
        Assert.Equal("accounting.view", source.RequiredPermission);
    }

    /// <summary>
    /// A year of trading, every entry balanced. The comments in the tests above
    /// work their figures from these lines.
    /// </summary>
    private static Books AYearOfTrading()
    {
        Books books = new();

        // Before the period.
        books.Post(new DateOnly(2026, 1, 1), (Bank, 100_000m), (Capital, -100_000m));
        books.Post(new DateOnly(2026, 3, 1), (Receivable, 20_000m), (Sales, -20_000m));
        books.Post(new DateOnly(2026, 3, 1), (Inventory, 10_000m), (Payable, -10_000m));

        // Within it.
        books.Post(new DateOnly(2026, 5, 1),
            (Receivable, 118_000m), (Sales, -100_000m), (OutputGst, -18_000m));
        books.Post(new DateOnly(2026, 5, 20), (SalesReturns, 5_000m), (Receivable, -5_000m));
        books.Post(new DateOnly(2026, 6, 1), (CostOfGoodsSold, 60_000m), (Inventory, -60_000m));
        books.Post(new DateOnly(2026, 6, 5), (Inventory, 50_000m), (Payable, -50_000m));
        books.Post(new DateOnly(2026, 6, 20), (Payable, 2_000m), (PurchaseReturns, -2_000m));
        books.Post(new DateOnly(2026, 7, 1), (Rent, 12_000m), (Bank, -12_000m));
        books.Post(new DateOnly(2026, 7, 15), (Bank, 1_000m), (FxGain, -1_000m));
        books.Post(new DateOnly(2026, 8, 1), (Bank, 90_000m), (Receivable, -90_000m));
        books.Post(new DateOnly(2026, 8, 10), (Payable, 40_000m), (Bank, -40_000m));
        books.Post(new DateOnly(2026, 9, 1), (Plant, 30_000m), (Bank, -30_000m));
        books.Post(new DateOnly(2027, 3, 31), (DepreciationExpense, 3_000m), (PlantDepreciation, -3_000m));
        books.Post(new DateOnly(2026, 10, 1), (FixedAssetHolding, 5_000m), (Payable, -5_000m));

        // After it: counts nowhere.
        books.Post(new DateOnly(2027, 4, 5), (Receivable, 999m), (Sales, -999m));

        return books;
    }

    private sealed class Books
    {
        private readonly List<JournalLedgerRead> _ledger = [];

        private readonly List<AccountRead> _accounts =
        [
            Account(Bank, Asset, isBank: true),
            Account(Receivable, Asset, "Accounts Receivable"),
            Account(Inventory, Asset, "Inventory"),
            Account(Plant, Asset),
            Account(PlantDepreciation, Asset),
            Account(FixedAssetHolding, Asset, "Fixed Asset"),
            Account(Payable, Liability, "Accounts Payable"),
            Account(OutputGst, Liability, "Output GST"),
            Account(Capital, Equity),
            Account(Sales, Income, "Sales Revenue", isSales: true),
            Account(SalesReturns, Income, "Sales Returns", isSales: true),
            Account(FxGain, Income, "Realized FX Gain/Loss"),
            Account(CostOfGoodsSold, Expense, "Cost of Goods Sold"),
            Account(PurchaseReturns, Expense, "Purchase Returns"),
            Account(Rent, Expense),
            Account(DepreciationExpense, Expense),
        ];

        private readonly List<FixedAssetCategoryRead> _categories =
        [
            new()
            {
                FixedAssetCategoryId = 1,
                CategoryName = "Plant",
                AssetAccountId = Plant,
                AccumulatedDepreciationAccountId = PlantDepreciation,
                DepreciationExpenseAccountId = DepreciationExpense,
            },
        ];

        /// <summary>One entry: a positive amount is a debit, a negative one a credit.</summary>
        public void Post(DateOnly on, params (long Account, decimal Amount)[] lines)
        {
            Assert.Equal(0m, lines.Sum(l => l.Amount));

            foreach ((long account, decimal amount) in lines)
            {
                _ledger.Add(new JournalLedgerRead
                {
                    LedgerId = _ledger.Count + 1,
                    AccountId = account,
                    LedgerDate = on,
                    DebitAmountBase = amount > 0 ? amount : 0m,
                    CreditAmountBase = amount < 0 ? -amount : 0m,
                    TransactionTypeCode = "JRN",
                    CurrencyCode = "INR",
                });
            }
        }

        public List<BusinessPerformanceRow> Rows(DateOnly start, DateOnly end) =>
            [.. BusinessPerformanceSource.Rows(
                _accounts.AsQueryable(),
                _ledger.AsQueryable(),
                _categories.AsQueryable(),
                start,
                end)];

        public BusinessPerformanceRow Metric(BusinessPerformanceMetric metric) =>
            Metric(metric, Start, End);

        public BusinessPerformanceRow Metric(
            BusinessPerformanceMetric metric, DateOnly start, DateOnly end) =>
            Assert.Single(Rows(start, end), r => r.MetricOrder == (int)metric);

        private static AccountRead Account(
            long id, int type, string? systemName = null, bool isBank = false, bool isSales = false) =>
            new()
            {
                AccountId = id,
                AccountTypeId = type,
                AccountCode = id.ToString(),
                AccountName = systemName ?? $"Account {id}",
                AccountSystemName = systemName,
                IsBank = isBank,
                IsSales = isSales,
                IsActive = true,
            };
    }
}
