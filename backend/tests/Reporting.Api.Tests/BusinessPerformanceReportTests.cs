using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// Business Performance's own page (TK-203). The ratio arithmetic is the catalog
/// source's and is tested in <see cref="BusinessPerformanceTests"/>; these tests
/// cover what this report adds: every ratio named, grouped and in order, a
/// comparison period and the change, and the request, run over the source's
/// rows from a small set of books.
/// </summary>
public sealed class BusinessPerformanceReportTests
{
    private static readonly DateOnly From = new(2026, 4, 1);
    private static readonly DateOnly To = new(2026, 9, 30); // 183 days
    private static readonly DateOnly CompareFrom = new(2025, 4, 1);
    private static readonly DateOnly CompareTo = new(2025, 9, 30);

    private const int Asset = 1, Liability = 2, Equity = 3, Income = 4, Expense = 5;
    private const long Bank = 1, Receivable = 2, Payable = 3, Capital = 4, Sales = 5, CostOfGoodsSold = 6, Rent = 7, Inventory = 8;

    private static AccountRead Account(long id, int type, string? systemName = null, bool isSales = false, bool isBank = false) =>
        new()
        {
            AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = systemName ?? $"Account {id}",
            AccountSystemName = systemName, IsSales = isSales, IsBank = isBank, IsActive = true,
        };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Bank, Asset, isBank: true),
        Account(Receivable, Asset, "Accounts Receivable"),
        Account(Inventory, Asset, "Inventory"),
        Account(Payable, Liability, "Accounts Payable"),
        Account(Capital, Equity),
        Account(Sales, Income, "Sales Revenue", isSales: true),
        Account(CostOfGoodsSold, Expense, "Cost of Goods Sold"),
        Account(Rent, Expense),
    ];

    private static List<JournalLedgerRead> Books()
    {
        List<JournalLedgerRead> ledger = [];
        void Post(DateOnly on, params (long Account, decimal Amount)[] lines)
        {
            foreach ((long account, decimal amount) in lines)
            {
                ledger.Add(new JournalLedgerRead
                {
                    LedgerId = ledger.Count + 1, AccountId = account, LedgerDate = on, TransactionTypeCode = "JRN", CurrencyCode = "INR",
                    DebitAmountBase = amount > 0 ? amount : 0m, CreditAmountBase = amount < 0 ? -amount : 0m,
                });
            }
        }

        Post(new(2025, 1, 1), (Bank, 100_000m), (Capital, -100_000m));
        // 2025: sales 40,000 on credit, cost 24,000, rent 6,000; 30,000 collected.
        Post(new(2025, 5, 1), (Receivable, 40_000m), (Sales, -40_000m));
        Post(new(2025, 5, 2), (CostOfGoodsSold, 24_000m), (Inventory, -24_000m));
        Post(new(2025, 6, 1), (Rent, 6_000m), (Bank, -6_000m));
        Post(new(2025, 8, 1), (Bank, 30_000m), (Receivable, -30_000m));
        // 2026: sales 80,000 on credit, cost 44,000, rent 12,000; stock bought on credit.
        Post(new(2026, 5, 1), (Receivable, 80_000m), (Sales, -80_000m));
        Post(new(2026, 5, 2), (CostOfGoodsSold, 44_000m), (Inventory, -44_000m));
        Post(new(2026, 5, 10), (Inventory, 50_000m), (Payable, -50_000m));
        Post(new(2026, 6, 1), (Rent, 12_000m), (Bank, -12_000m));
        return ledger;
    }

    private static List<BusinessPerformanceRow> Rows(DateOnly from, DateOnly to, List<JournalLedgerRead>? ledger = null) =>
    [
        .. BusinessPerformanceSourceRepository.Rows(
            Accounts.AsQueryable(), (ledger ?? Books()).AsQueryable(), new List<FixedAssetCategoryRead>().AsQueryable(), from, to),
    ];

    private static BusinessPerformanceReportResultDto Report(bool compare = false, List<JournalLedgerRead>? ledger = null) =>
        BusinessPerformanceReportRepository.Compose(
            Rows(From, To, ledger), compare ? Rows(CompareFrom, CompareTo, ledger) : null,
            From, To, compare ? CompareFrom : null, compare ? CompareTo : null);

    private static BusinessPerformanceRatioDto Ratio(BusinessPerformanceReportResultDto report, BusinessPerformanceMetric metric) =>
        report.Ratios.Single(r => r.Metric == metric);

    // ---- The ratios ----------------------------------------------------------------

    [Fact]
    public void Every_ratio_is_listed_once_in_its_defined_order_with_its_group()
    {
        BusinessPerformanceReportResultDto report = Report();

        Assert.Equal(Enum.GetValues<BusinessPerformanceMetric>().OrderBy(m => (int)m), report.Ratios.Select(r => r.Metric));
        Assert.Equal(
            [
                BusinessPerformanceGroup.Profitability, BusinessPerformanceGroup.Profitability, BusinessPerformanceGroup.Profitability,
                BusinessPerformanceGroup.Efficiency, BusinessPerformanceGroup.Efficiency,
                BusinessPerformanceGroup.FinancialPosition, BusinessPerformanceGroup.FinancialPosition,
                BusinessPerformanceGroup.Cash,
            ],
            report.Ratios.Select(r => r.Group));
    }

    [Fact]
    public void Each_ratio_is_named_and_explained_as_the_catalog_report_names_and_explains_it()
    {
        List<BusinessPerformanceRow> rows = Rows(From, To);

        Assert.All(Report().Ratios, ratio =>
        {
            BusinessPerformanceRow row = rows.Single(r => r.MetricOrder == (int)ratio.Metric);
            Assert.Equal(row.Metric, ratio.Title);
            Assert.Equal(row.Calculation, ratio.Calculation);
            Assert.Equal(row.Unit, ratio.Unit.ToString());
        });
    }

    [Fact]
    public void Each_value_is_its_numerator_over_its_denominator_in_its_unit()
    {
        BusinessPerformanceReportResultDto report = Report();

        // Gross: (80,000 − 44,000) ÷ 80,000 = 45%. Net: (80,000 − 56,000) ÷ 80,000 = 30%.
        Assert.Equal(45m, Ratio(report, BusinessPerformanceMetric.GrossProfitMargin).Value);
        Assert.Equal(36_000m, Ratio(report, BusinessPerformanceMetric.GrossProfitMargin).Numerator);
        Assert.Equal(80_000m, Ratio(report, BusinessPerformanceMetric.GrossProfitMargin).Denominator);
        Assert.Equal(30m, Ratio(report, BusinessPerformanceMetric.NetProfitMargin).Value);

        foreach (BusinessPerformanceRatioDto ratio in report.Ratios.Where(r => r.Unit is BusinessPerformanceUnit.Percent or BusinessPerformanceUnit.Times
            && r.Metric is not BusinessPerformanceMetric.ReturnOnInvestment && r.Value is not null))
        {
            decimal expected = ratio.Numerator!.Value / ratio.Denominator!.Value * (ratio.Unit == BusinessPerformanceUnit.Percent ? 100m : 1m);
            Assert.Equal(Math.Round(expected, 2), ratio.Value);
        }

        // Cash is an amount, not a ratio: 100,000 − 6,000 + 30,000 − 12,000.
        BusinessPerformanceRatioDto cash = Ratio(report, BusinessPerformanceMetric.TotalCashBalance);
        Assert.Equal(112_000m, cash.Value);
        Assert.Null(cash.Denominator);
        Assert.Equal(BusinessPerformanceUnit.Amount, cash.Unit);
    }

    [Fact]
    public void Customer_days_scale_by_the_days_in_the_period()
    {
        BusinessPerformanceReportResultDto report = Report();
        BusinessPerformanceRatioDto days = Ratio(report, BusinessPerformanceMetric.AverageDaysCustomersTakeToPay);

        // Receivables 10,000 at the start and 90,000 at the end, against credit sales of 80,000 over 183 days.
        Assert.Equal(183, report.Days);
        Assert.Equal(50_000m, days.Numerator);
        Assert.Equal(80_000m, days.Denominator);
        Assert.Equal(Math.Round(50_000m / 80_000m * 183m, 2), days.Value);
        Assert.Equal(BusinessPerformanceUnit.Days, days.Unit);
    }

    [Fact]
    public void A_ratio_with_nothing_to_divide_by_is_listed_with_no_value()
    {
        // No credit purchases in 2025, so supplier days cannot be worked out.
        BusinessPerformanceRatioDto supplierDays = Ratio(
            BusinessPerformanceReportRepository.Compose(Rows(CompareFrom, CompareTo), null, CompareFrom, CompareTo, null, null),
            BusinessPerformanceMetric.AverageDaysToPaySuppliers);

        Assert.Null(supplierDays.Value);
        Assert.Equal(0m, supplierDays.Denominator);
    }

    [Fact]
    public void Books_with_nothing_posted_still_list_every_ratio_with_no_value()
    {
        BusinessPerformanceReportResultDto report = Report(ledger: []);

        Assert.Equal(8, report.Ratios.Count);
        Assert.All(report.Ratios, r =>
        {
            Assert.Null(r.Value);
            Assert.Null(r.Numerator);
            Assert.False(string.IsNullOrEmpty(r.Title));
        });
    }

    // ---- The comparison ------------------------------------------------------------

    [Fact]
    public void The_comparison_period_has_its_own_values_and_the_change_is_in_the_ratios_unit()
    {
        BusinessPerformanceReportResultDto report = Report(compare: true);
        BusinessPerformanceRatioDto gross = Ratio(report, BusinessPerformanceMetric.GrossProfitMargin);

        // 2025: (40,000 − 24,000) ÷ 40,000 = 40%; five points less than 2026's 45%.
        Assert.Equal(40m, gross.CompareValue);
        Assert.Equal(16_000m, gross.CompareNumerator);
        Assert.Equal(40_000m, gross.CompareDenominator);
        Assert.Equal(5m, gross.Change);
        Assert.Equal(CompareFrom, report.CompareFromDate);
        Assert.Equal(183, report.CompareDays);

        // The period's own figures do not move for being compared.
        Assert.Equal(45m, gross.Value);
    }

    [Fact]
    public void There_is_no_change_where_either_period_has_no_value()
    {
        BusinessPerformanceRatioDto supplierDays = Ratio(Report(compare: true), BusinessPerformanceMetric.AverageDaysToPaySuppliers);

        Assert.NotNull(supplierDays.Value);
        Assert.Null(supplierDays.CompareValue);
        Assert.Null(supplierDays.Change);
    }

    [Fact]
    public void Without_a_comparison_period_there_are_no_comparison_figures()
    {
        BusinessPerformanceReportResultDto report = Report();

        Assert.All(report.Ratios, r =>
        {
            Assert.Null(r.CompareValue);
            Assert.Null(r.CompareNumerator);
            Assert.Null(r.Change);
        });
        Assert.Null(report.CompareFromDate);
        Assert.Null(report.CompareDays);
    }

    [Theory]
    [InlineData(BusinessPerformanceMetric.GrossProfitMargin, BusinessPerformanceGroup.Profitability)]
    [InlineData(BusinessPerformanceMetric.ReturnOnInvestment, BusinessPerformanceGroup.Profitability)]
    [InlineData(BusinessPerformanceMetric.AverageDaysToPaySuppliers, BusinessPerformanceGroup.Efficiency)]
    [InlineData(BusinessPerformanceMetric.TermAssetsToLiabilities, BusinessPerformanceGroup.FinancialPosition)]
    [InlineData(BusinessPerformanceMetric.TotalCashBalance, BusinessPerformanceGroup.Cash)]
    public void Each_ratio_has_its_group(BusinessPerformanceMetric metric, BusinessPerformanceGroup group) =>
        Assert.Equal(group, BusinessPerformanceReportRepository.GroupOf(metric));

    [Fact]
    public void Days_count_both_ends() =>
        Assert.Equal(365, BusinessPerformanceReportRepository.DaysIn(new(2026, 4, 1), new(2027, 3, 31)));

    // ---- The request and the endpoint ----------------------------------------------

    [Fact]
    public void A_period_and_a_comparison_period_in_order_are_accepted() =>
        Assert.Empty(BusinessPerformanceReportRepository.DateRefusals(new BusinessPerformanceReportRequest
        {
            FromDate = From, ToDate = To, CompareFromDate = CompareFrom, CompareToDate = CompareTo,
        }));

    [Fact]
    public void Each_date_mistake_is_refused_at_the_field_it_concerns()
    {
        static string Field(BusinessPerformanceReportRequest request) =>
            Assert.Single(BusinessPerformanceReportRepository.DateRefusals(request)).Field;

        Assert.Equal(nameof(BusinessPerformanceReportRequest.ToDate), Field(new() { FromDate = To, ToDate = From }));
        Assert.Equal(nameof(BusinessPerformanceReportRequest.CompareToDate), Field(new() { FromDate = From, ToDate = To, CompareFromDate = CompareFrom }));
        Assert.Equal(nameof(BusinessPerformanceReportRequest.CompareFromDate), Field(new() { FromDate = From, ToDate = To, CompareToDate = CompareTo }));
        Assert.Equal(
            nameof(BusinessPerformanceReportRequest.CompareToDate),
            Field(new() { FromDate = From, ToDate = To, CompareFromDate = CompareTo, CompareToDate = CompareFrom }));
    }

    [Fact]
    public void The_from_and_to_dates_are_required_by_the_request_itself()
    {
        Assert.Equal(
            [nameof(BusinessPerformanceReportRequest.FromDate), nameof(BusinessPerformanceReportRequest.ToDate)],
            typeof(BusinessPerformanceReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BusinessPerformanceReportController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("org_id", Guid.NewGuid().ToString())], "test")),
                },
            },
        };

        IActionResult result = await controller.Run(
            new BusinessPerformanceReportRequest { FromDate = From, ToDate = To, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_has_its_address_and_needs_reports_and_accounting_view()
    {
        Type controller = typeof(BusinessPerformanceReportController);

        // The gateway routes this exact path to Reporting; the page calls it.
        Assert.Equal("api/statements/business-performance", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(BusinessPerformanceReportController.Run))!
                .GetCustomAttributes<RequirePermissionAttribute>()
                .Select(a => a.Permission));
    }
}
