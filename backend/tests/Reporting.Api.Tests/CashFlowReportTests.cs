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
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Api.Tests;

/// <summary>
/// The Cash Flow Statement – Direct (TK-201), on a small ledger worked by
/// hand: April to September 2026, compared with April to September 2025.
/// Every posting balances, so the statement must reconcile; one test breaks
/// that on purpose.
///
/// The nets and balances run on a list through the same expressions the
/// repository runs against <c>acc.JournalLedger</c>; the composition is pure.
/// </summary>
public sealed class CashFlowReportTests
{
    private static readonly DateOnly From = new(2026, 4, 1);
    private static readonly DateOnly To = new(2026, 9, 30);
    private static readonly DateOnly CompareFrom = new(2025, 4, 1);
    private static readonly DateOnly CompareTo = new(2025, 9, 30);

    private const int Control = 3, Item = 1, Tax = 2, Cogs = 4, Gdni = 7;
    private const int Document = 1, OpeningBalance = 13;

    private static readonly List<AccountTypeDto> Types =
    [
        new(1, "Asset", "Asset", "Debit", "BalanceSheet", 1),
        new(2, "Liability", "Liability", "Credit", "BalanceSheet", 2),
        new(3, "Equity", "Equity", "Credit", "BalanceSheet", 3),
        new(4, "Income", "Income", "Credit", "ProfitAndLoss", 4),
        new(5, "Expense", "Expense", "Debit", "ProfitAndLoss", 5),
    ];

    private static CashFlowAccount Account(
        long id, int type, string name, string? systemName = null, bool bank = false, bool sales = false) =>
        new()
        {
            AccountId = id,
            AccountTypeId = type,
            AccountCode = id.ToString(),
            AccountName = name,
            AccountSystemName = systemName,
            IsBank = bank,
            IsSales = sales,
        };

    private static readonly List<CashFlowAccount> Accounts =
    [
        Account(1501, 1, "Main Bank", bank: true),
        Account(1401, 1, "Petty Cash", bank: true),
        Account(2301, 2, "Company Card", bank: true),
        Account(1100, 1, "Accounts Receivable", "Accounts Receivable"),
        Account(1200, 1, "Inventory", "Inventory"),
        Account(1300, 1, "Input GST", "Input GST"),
        Account(1310, 1, "TDS Receivable", "TDS Receivable"),
        Account(1600, 1, "Fixed Asset", "Fixed Asset"),
        Account(1610, 1, "Vehicles"),
        Account(1615, 1, "Vehicles Depreciation"),
        Account(2100, 2, "Accounts Payable", "Accounts Payable"),
        Account(2120, 2, "Salary Payable", "Salary Payable"),
        Account(2200, 2, "Output GST", "Output GST"),
        Account(2400, 2, "Refundable Deposits", "Refundable Deposits"),
        Account(3100, 3, "Opening Balance Equity", "Opening Balance Equity"),
        Account(3200, 3, "Capital"),
        Account(4100, 4, "Sales Revenue", "Sales Revenue", sales: true),
        Account(4300, 4, "Interest Income"),
        Account(4900, 4, "Realized FX Gain/Loss", "Realized FX Gain/Loss"),
        Account(4910, 4, "Unrealized FX Gain/Loss", "Unrealized FX Gain/Loss"),
        Account(4920, 4, "Gain/Loss on Asset Disposal", "Gain/Loss on Asset Disposal"),
        Account(5100, 5, "Cost of Goods Sold", "Cost of Goods Sold"),
        Account(5300, 5, "Rent"),
        Account(5900, 5, "Round Off", "Round Off"),
    ];

    /// <summary>The vehicles category posts cost to 1610 and depreciation to 1615.</summary>
    private static readonly HashSet<long> FixedAssetAccounts = [1610, 1615];

    private static long _leg;

    private static JournalLedgerRead Leg(
        string type, long id, DateOnly date, long account, decimal debit, decimal credit,
        int ledgerType = Control, int source = Document) =>
        new()
        {
            LedgerId = ++_leg,
            TransactionTypeCode = type,
            TransactionId = id,
            LedgerDate = date,
            AccountId = account,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
            LedgerTypeId = ledgerType,
            LedgerSourceId = source,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // Before the period: capital paid in.
        Leg("JRN", 2, new(2026, 3, 15), 1501, 10_000m, 0m), Leg("JRN", 2, new(2026, 3, 15), 3200, 0m, 10_000m),
        // The bank's opening balance, entered on the first day: opening cash, not a flow.
        Leg("OPB", 1, From, 1501, 100_000m, 0m, source: OpeningBalance),
        Leg("OPB", 1, From, 3100, 0m, 100_000m, source: OpeningBalance),
        // A cash sale with GST, and its cost-of-sales pair.
        Leg("INV", 3, new(2026, 4, 10), 1501, 11_800m, 0m),
        Leg("INV", 3, new(2026, 4, 10), 4100, 0m, 10_000m, Item),
        Leg("INV", 3, new(2026, 4, 10), 2200, 0m, 1_800m, Tax),
        Leg("INV", 3, new(2026, 4, 10), 5100, 6_000m, 0m, Cogs),
        Leg("INV", 3, new(2026, 4, 10), 1200, 0m, 6_000m, Cogs),
        // A sale on credit: no cash.
        Leg("INV", 4, new(2026, 4, 20), 1100, 23_600m, 0m),
        Leg("INV", 4, new(2026, 4, 20), 4100, 0m, 20_000m, Item),
        Leg("INV", 4, new(2026, 4, 20), 2200, 0m, 3_600m, Tax),
        // The customer pays it, less TDS.
        Leg("RCV", 5, new(2026, 5, 5), 1501, 23_128m, 0m),
        Leg("RCV", 5, new(2026, 5, 5), 1310, 472m, 0m),
        Leg("RCV", 5, new(2026, 5, 5), 1100, 0m, 23_600m),
        // A purchase on credit: no cash.
        Leg("BIL", 6, new(2026, 5, 10), 1200, 30_000m, 0m, Item),
        Leg("BIL", 6, new(2026, 5, 10), 1300, 5_400m, 0m, Tax),
        Leg("BIL", 6, new(2026, 5, 10), 2100, 0m, 35_400m),
        // The supplier is paid.
        Leg("SPD", 7, new(2026, 5, 25), 2100, 35_400m, 0m), Leg("SPD", 7, new(2026, 5, 25), 1501, 0m, 35_400m),
        // Rent with GST, rounded.
        Leg("SPD", 8, new(2026, 6, 1), 5300, 12_000m, 0m),
        Leg("SPD", 8, new(2026, 6, 1), 1300, 2_160m, 0m),
        Leg("SPD", 8, new(2026, 6, 1), 5900, 0m, 0.40m),
        Leg("SPD", 8, new(2026, 6, 1), 1501, 0m, 14_159.60m),
        // Salaries paid.
        Leg("JRN", 9, new(2026, 6, 30), 2120, 25_000m, 0m), Leg("JRN", 9, new(2026, 6, 30), 1501, 0m, 25_000m),
        // GST paid to the government.
        Leg("JRN", 10, new(2026, 7, 20), 2200, 3_000m, 0m), Leg("JRN", 10, new(2026, 7, 20), 1501, 0m, 3_000m),
        // Interest received, and a deposit taken.
        Leg("RCV", 11, new(2026, 8, 1), 1501, 500m, 0m), Leg("RCV", 11, new(2026, 8, 1), 4300, 0m, 500m),
        Leg("RCV", 12, new(2026, 8, 5), 1501, 2_000m, 0m), Leg("RCV", 12, new(2026, 8, 5), 2400, 0m, 2_000m),
        // A vehicle bought, with GST.
        Leg("SPD", 13, new(2026, 8, 10), 1610, 40_000m, 0m),
        Leg("SPD", 13, new(2026, 8, 10), 1300, 7_200m, 0m),
        Leg("SPD", 13, new(2026, 8, 10), 1501, 0m, 47_200m),
        // An old vehicle sold at a gain.
        Leg("JRN", 14, new(2026, 9, 1), 1501, 15_000m, 0m),
        Leg("JRN", 14, new(2026, 9, 1), 1615, 8_000m, 0m),
        Leg("JRN", 14, new(2026, 9, 1), 1610, 0m, 20_000m),
        Leg("JRN", 14, new(2026, 9, 1), 4920, 0m, 3_000m),
        // Drawings.
        Leg("JRN", 15, new(2026, 9, 10), 3200, 5_000m, 0m), Leg("JRN", 15, new(2026, 9, 10), 1501, 0m, 5_000m),
        // From the bank to petty cash: no flow.
        Leg("TRF", 16, new(2026, 9, 15), 1401, 1_000m, 0m), Leg("TRF", 16, new(2026, 9, 15), 1501, 0m, 1_000m),
        // Rent and a deposit on the card, with GST: two lines, so the GST stands alone.
        Leg("SPD", 17, new(2026, 9, 20), 5300, 1_000m, 0m),
        Leg("SPD", 17, new(2026, 9, 20), 2400, 500m, 0m),
        Leg("SPD", 17, new(2026, 9, 20), 1300, 180m, 0m),
        Leg("SPD", 17, new(2026, 9, 20), 2301, 0m, 1_680m),
        // A foreign customer pays, with a realized gain.
        Leg("RCV", 19, new(2026, 9, 25), 1501, 10_300m, 0m),
        Leg("RCV", 19, new(2026, 9, 25), 1100, 0m, 10_000m),
        Leg("RCV", 19, new(2026, 9, 25), 4900, 0m, 300m),
        // The foreign-currency bank account revalued.
        Leg("JRN", 18, To, 1501, 250m, 0m), Leg("JRN", 18, To, 4910, 0m, 250m),
        // After the period.
        Leg("RCV", 20, new(2026, 10, 1), 1501, 999m, 0m), Leg("RCV", 20, new(2026, 10, 1), 4100, 0m, 999m),

        // 2025: capital before the comparison period, then a cash sale and rent.
        Leg("JRN", 100, new(2025, 2, 1), 1501, 3_000m, 0m), Leg("JRN", 100, new(2025, 2, 1), 3200, 0m, 3_000m),
        Leg("INV", 101, new(2025, 5, 1), 1501, 5_900m, 0m),
        Leg("INV", 101, new(2025, 5, 1), 4100, 0m, 5_000m, Item),
        Leg("INV", 101, new(2025, 5, 1), 2200, 0m, 900m, Tax),
        Leg("SPD", 102, new(2025, 6, 1), 5300, 4_000m, 0m), Leg("SPD", 102, new(2025, 6, 1), 1501, 0m, 4_000m),
    ];

    private static readonly long[] Cash = [1501, 1401, 2301];

    private static List<PostingAccountNet> Nets(IEnumerable<JournalLedgerRead> ledger, bool compare) =>
    [
        .. CashFlowReportRepository.PostingNets(
            ledger.AsQueryable(), Cash, compare ? CompareFrom : From, To),
    ];

    private static CashBalances Balances(IEnumerable<JournalLedgerRead> ledger, bool compare) =>
        CashFlowReportRepository.Balances(
            ledger.AsQueryable(), Cash, From, To, compare ? CompareFrom : null, compare ? CompareTo : null)
            .FirstOrDefault() ?? new CashBalances();

    private static CashFlowReportResultDto Report(bool compare = false, IEnumerable<JournalLedgerRead>? ledger = null)
    {
        List<JournalLedgerRead> legs = [.. ledger ?? Ledger];
        return CashFlowReportRepository.Compose(
            Accounts, Types, FixedAssetAccounts, Nets(legs, compare), Balances(legs, compare),
            From, To, compare ? CompareFrom : null, compare ? CompareTo : null);
    }

    private static CashFlowLineDto Line(CashFlowReportResultDto report, CashFlowLine line) =>
        report.Sections.SelectMany(s => s.Lines).Single(l => l.Line == line);

    private static decimal AccountIn(CashFlowReportResultDto report, CashFlowLine line, long accountId) =>
        Line(report, line).Accounts.Single(a => a.AccountId == accountId).Amount;

    private static CashFlowSectionDto Section(CashFlowReportResultDto report, CashFlowActivity activity) =>
        report.Sections.Single(s => s.Activity == activity);

    // ---- The postings ------------------------------------------------------------

    [Fact]
    public void Only_postings_that_move_cash_are_read()
    {
        List<PostingAccountNet> nets = Nets(Ledger, compare: false);

        Assert.DoesNotContain(nets, n => n.TransactionTypeCode == "INV" && n.TransactionId == 4); // a sale on credit
        Assert.DoesNotContain(nets, n => n.TransactionTypeCode == "BIL"); // a purchase on credit
        Assert.DoesNotContain(nets, n => n.TransactionId is 2 or 20 or 101); // outside the dates
    }

    [Fact]
    public void Opening_balances_and_cost_of_sales_pairs_are_not_flows()
    {
        List<PostingAccountNet> nets = Nets(Ledger, compare: false);

        Assert.DoesNotContain(nets, n => n.TransactionTypeCode == "OPB");
        Assert.Equal(
            [1501L, 2200L, 4100L],
            nets.Where(n => n.TransactionTypeCode == "INV" && n.TransactionId == 3).Select(n => n.AccountId).Order());
    }

    [Fact]
    public void Each_account_is_netted_within_its_posting_as_credits_less_debits()
    {
        List<PostingAccountNet> receipt = [.. Nets(Ledger, compare: false).Where(n => n.TransactionId == 5)];

        Assert.Equal(23_600m, receipt.Single(n => n.AccountId == 1100).Net);
        Assert.Equal(-472m, receipt.Single(n => n.AccountId == 1310).Net);
        Assert.Equal(-23_128m, receipt.Single(n => n.AccountId == 1501).Net);
    }

    [Fact]
    public void Opening_cash_includes_opening_balances_entered_within_the_period()
    {
        CashBalances balances = Balances(Ledger, compare: false);

        // 10,000 and 2025's 4,900 before the period, and the 1 April opening balance of 100,000.
        Assert.Equal(114_900m, balances.Opening);
        Assert.Equal(46_438.40m, balances.Closing); // the 999 on 1 October is after the period
    }

    // ---- The lines ---------------------------------------------------------------

    [Fact]
    public void A_customer_receipt_counts_in_full_with_its_gst_tds_and_exchange_gain()
    {
        CashFlowReportResultDto report = Report();
        CashFlowLineDto receipts = Line(report, CashFlowLine.ReceiptsFromCustomers);

        // 11,800 for the cash sale, 23,128 after TDS, and 10,300 from abroad.
        Assert.Equal(45_228m, receipts.Amount);
        Assert.Equal(10_000m, AccountIn(report, CashFlowLine.ReceiptsFromCustomers, 4100));
        Assert.Equal(1_800m, AccountIn(report, CashFlowLine.ReceiptsFromCustomers, 2200));
        Assert.Equal(33_600m, AccountIn(report, CashFlowLine.ReceiptsFromCustomers, 1100));
        Assert.Equal(-472m, AccountIn(report, CashFlowLine.ReceiptsFromCustomers, 1310));
        Assert.Equal(300m, AccountIn(report, CashFlowLine.ReceiptsFromCustomers, 4900));
    }

    [Fact]
    public void Supplier_rent_and_salary_payments_count_with_their_gst_and_rounding()
    {
        CashFlowReportResultDto report = Report();

        Assert.Equal(-75_559.60m, Line(report, CashFlowLine.PaymentsToSuppliersAndEmployees).Amount);
        Assert.Equal(-35_400m, AccountIn(report, CashFlowLine.PaymentsToSuppliersAndEmployees, 2100));
        Assert.Equal(-13_000m, AccountIn(report, CashFlowLine.PaymentsToSuppliersAndEmployees, 5300));
        Assert.Equal(-2_160m, AccountIn(report, CashFlowLine.PaymentsToSuppliersAndEmployees, 1300));
        Assert.Equal(0.40m, AccountIn(report, CashFlowLine.PaymentsToSuppliersAndEmployees, 5900));
        Assert.Equal(-25_000m, AccountIn(report, CashFlowLine.PaymentsToSuppliersAndEmployees, 2120));
    }

    [Fact]
    public void Gst_paid_to_the_government_or_beside_two_lines_keeps_its_own_line()
    {
        CashFlowReportResultDto report = Report();

        Assert.Equal(-3_180m, Line(report, CashFlowLine.GstAndTds).Amount);
        Assert.Equal(-3_000m, AccountIn(report, CashFlowLine.GstAndTds, 2200));
        Assert.Equal(-180m, AccountIn(report, CashFlowLine.GstAndTds, 1300));
    }

    [Fact]
    public void Other_income_and_liabilities_are_other_operating_activities()
    {
        CashFlowReportResultDto report = Report();

        Assert.Equal(2_000m, Line(report, CashFlowLine.OtherOperating).Amount);
        Assert.Equal(500m, AccountIn(report, CashFlowLine.OtherOperating, 4300));
        Assert.Equal(1_500m, AccountIn(report, CashFlowLine.OtherOperating, 2400)); // 2,000 taken, 500 paid
    }

    [Fact]
    public void A_fixed_asset_bought_is_a_purchase_with_its_gst_and_one_sold_is_a_sale_with_its_gain()
    {
        CashFlowReportResultDto report = Report();

        Assert.Equal(-47_200m, Line(report, CashFlowLine.FixedAssetPurchases).Amount);
        Assert.Equal(-7_200m, AccountIn(report, CashFlowLine.FixedAssetPurchases, 1300));

        // Proceeds of 15,000: cost 20,000 removed, depreciation of 8,000 written back, a gain of 3,000.
        Assert.Equal(15_000m, Line(report, CashFlowLine.FixedAssetSales).Amount);
        Assert.Equal(20_000m, AccountIn(report, CashFlowLine.FixedAssetSales, 1610));
        Assert.Equal(-8_000m, AccountIn(report, CashFlowLine.FixedAssetSales, 1615));
        Assert.Equal(3_000m, AccountIn(report, CashFlowLine.FixedAssetSales, 4920));
        Assert.Equal(-32_200m, Section(report, CashFlowActivity.Investing).Total);
    }

    [Fact]
    public void Drawings_are_financing_and_opening_balance_equity_is_never_a_flow()
    {
        CashFlowReportResultDto report = Report();

        Assert.Equal(-5_000m, Section(report, CashFlowActivity.Financing).Total);
        Assert.DoesNotContain(report.Sections.SelectMany(s => s.Lines).SelectMany(l => l.Accounts), a => a.AccountId == 3100);
    }

    [Fact]
    public void A_transfer_between_cash_accounts_is_no_flow_and_cash_accounts_are_never_listed()
    {
        CashFlowReportResultDto report = Report();

        Assert.DoesNotContain(
            report.Sections.SelectMany(s => s.Lines).SelectMany(l => l.Accounts),
            a => a.AccountId is 1501 or 1401 or 2301);
    }

    [Fact]
    public void Revaluing_a_foreign_currency_bank_account_is_the_exchange_rate_effect_not_a_flow()
    {
        CashFlowReportResultDto report = Report();

        Assert.Equal(250m, report.ExchangeRateEffect.Amount);
        Assert.Equal(4910, Assert.Single(report.ExchangeRateEffect.Accounts).AccountId);
        Assert.DoesNotContain(report.Sections.SelectMany(s => s.Lines).SelectMany(l => l.Accounts), a => a.AccountId == 4910);
    }

    [Fact]
    public void Every_line_is_shown_in_order_with_its_title_even_when_nothing_moved()
    {
        CashFlowReportResultDto report = Report(ledger: []);

        Assert.Equal(
            ["Operating activities", "Investing activities", "Financing activities"],
            report.Sections.Select(s => s.Title));
        Assert.Equal(
            ["Receipts from customers", "Payments to suppliers and employees", "GST and TDS paid or refunded",
             "Other operating activities", "Purchase of fixed assets", "Sale of fixed assets",
             "Capital introduced and drawings"],
            report.Sections.SelectMany(s => s.Lines).Select(l => l.Title));
        Assert.All(report.Sections.SelectMany(s => s.Lines), l => Assert.Empty(l.Accounts));
        Assert.True(report.IsReconciled);
    }

    [Fact]
    public void Accounts_under_a_line_come_in_code_order()
    {
        Assert.Equal(
            ["1100", "1310", "2200", "4100", "4900"],
            Line(Report(), CashFlowLine.ReceiptsFromCustomers).Accounts.Select(a => a.AccountCode));
    }

    // ---- The totals --------------------------------------------------------------

    [Fact]
    public void Opening_cash_plus_the_net_change_and_the_exchange_effect_is_closing_cash()
    {
        CashFlowReportResultDto report = Report();
        CashFlowTotalsDto totals = report.Totals;

        Assert.Equal(-31_511.60m, Section(report, CashFlowActivity.Operating).Total);
        Assert.Equal(-68_711.60m, totals.NetChange);
        Assert.Equal(114_900m, totals.OpeningCash);
        Assert.Equal(46_438.40m, totals.ClosingCash);
        Assert.Equal(0m, totals.Difference);
        Assert.True(report.IsReconciled);
    }

    [Fact]
    public void Each_section_total_is_the_sum_of_its_lines_and_each_line_of_its_accounts()
    {
        CashFlowReportResultDto report = Report(compare: true);

        Assert.All(report.Sections, s => Assert.Equal(s.Lines.Sum(l => l.Amount), s.Total));
        Assert.All(report.Sections.SelectMany(s => s.Lines), l => Assert.Equal(l.Accounts.Sum(a => a.Amount), l.Amount));
    }

    [Fact]
    public void The_comparison_period_has_its_own_flows_and_cash_at_each_end()
    {
        CashFlowReportResultDto report = Report(compare: true);
        CashFlowTotalsDto totals = report.Totals;

        Assert.Equal(5_900m, Line(report, CashFlowLine.ReceiptsFromCustomers).CompareAmount);
        Assert.Equal(-4_000m, Line(report, CashFlowLine.PaymentsToSuppliersAndEmployees).CompareAmount);
        Assert.Equal(0m, Line(report, CashFlowLine.FixedAssetSales).CompareAmount);
        Assert.Equal(1_900m, totals.CompareNetChange);
        Assert.Equal(3_000m, totals.CompareOpeningCash);
        Assert.Equal(4_900m, totals.CompareClosingCash);
        Assert.Equal(0m, totals.CompareDifference);
        Assert.Equal(CompareFrom, report.CompareFromDate);

        // The period's own figures do not move for being compared.
        Assert.Equal(-68_711.60m, totals.NetChange);
        Assert.True(report.IsReconciled);
    }

    [Fact]
    public void Without_a_comparison_period_there_are_no_comparison_figures()
    {
        CashFlowReportResultDto report = Report();

        Assert.All(report.Sections.SelectMany(s => s.Lines), l => Assert.Null(l.CompareAmount));
        Assert.All(report.Sections, s => Assert.Null(s.CompareTotal));
        Assert.Null(report.Totals.CompareClosingCash);
        Assert.Null(report.CompareFromDate);
    }

    [Fact]
    public void Cash_that_moved_with_nothing_on_the_other_side_is_shown_as_a_difference()
    {
        CashFlowReportResultDto report = Report(ledger: [.. Ledger, Leg("JRN", 99, new(2026, 7, 1), 1501, 100m, 0m)]);

        Assert.Equal(100m, report.Totals.Difference);
        Assert.False(report.IsReconciled);
    }

    // ---- The classification ------------------------------------------------------

    [Theory]
    [InlineData(1100L, CashFlowLine.ReceiptsFromCustomers, false)]
    [InlineData(4100L, CashFlowLine.ReceiptsFromCustomers, false)]
    [InlineData(2100L, CashFlowLine.PaymentsToSuppliersAndEmployees, false)]
    [InlineData(1200L, CashFlowLine.PaymentsToSuppliersAndEmployees, false)]
    [InlineData(5300L, CashFlowLine.PaymentsToSuppliersAndEmployees, false)]
    [InlineData(1300L, CashFlowLine.GstAndTds, true)]
    [InlineData(1310L, CashFlowLine.GstAndTds, true)]
    [InlineData(5900L, CashFlowLine.OtherOperating, true)]
    [InlineData(4900L, CashFlowLine.OtherOperating, true)]
    [InlineData(4300L, CashFlowLine.OtherOperating, false)]
    [InlineData(2400L, CashFlowLine.OtherOperating, false)]
    [InlineData(1600L, CashFlowLine.FixedAssetPurchases, false)]
    [InlineData(1615L, CashFlowLine.FixedAssetPurchases, false)]
    [InlineData(4920L, CashFlowLine.FixedAssetPurchases, false)]
    [InlineData(3200L, CashFlowLine.CapitalAndDrawings, false)]
    [InlineData(4910L, CashFlowLine.ExchangeRateEffect, false)]
    public void Each_account_has_its_line(long accountId, CashFlowLine expected, bool follows)
    {
        CashFlowAccount account = Accounts.Single(a => a.AccountId == accountId);

        Assert.Equal(
            (expected, follows),
            CashFlowReportRepository.Classify(account, Types.Single(t => t.AccountTypeId == account.AccountTypeId), FixedAssetAccounts));
    }

    [Fact]
    public void An_account_master_does_not_list_is_other_operating() =>
        Assert.Equal((CashFlowLine.OtherOperating, false), CashFlowReportRepository.Classify(null, null, FixedAssetAccounts));

    // ---- The dates ---------------------------------------------------------------

    [Fact]
    public void A_period_and_a_comparison_period_in_order_are_accepted() =>
        Assert.Empty(CashFlowReportRepository.DateRefusals(new CashFlowReportRequest
        {
            FromDate = From, ToDate = To, CompareFromDate = CompareFrom, CompareToDate = CompareTo,
        }));

    [Fact]
    public void A_to_date_before_the_from_date_is_refused_at_the_to_date() =>
        Assert.Equal(
            nameof(CashFlowReportRequest.ToDate),
            Assert.Single(CashFlowReportRepository.DateRefusals(new CashFlowReportRequest { FromDate = To, ToDate = From })).Field);

    [Fact]
    public void A_comparison_period_needs_both_dates_in_order()
    {
        Assert.Equal(
            nameof(CashFlowReportRequest.CompareToDate),
            Assert.Single(CashFlowReportRepository.DateRefusals(
                new CashFlowReportRequest { FromDate = From, ToDate = To, CompareFromDate = CompareFrom })).Field);
        Assert.Equal(
            nameof(CashFlowReportRequest.CompareFromDate),
            Assert.Single(CashFlowReportRepository.DateRefusals(
                new CashFlowReportRequest { FromDate = From, ToDate = To, CompareToDate = CompareTo })).Field);
        Assert.Equal(
            nameof(CashFlowReportRequest.CompareToDate),
            Assert.Single(CashFlowReportRepository.DateRefusals(new CashFlowReportRequest
            {
                FromDate = From, ToDate = To, CompareFromDate = CompareTo, CompareToDate = CompareFrom,
            })).Field);
    }

    [Fact]
    public void The_from_and_to_dates_are_required_by_the_request_itself()
    {
        Assert.Equal(
            [nameof(CashFlowReportRequest.FromDate), nameof(CashFlowReportRequest.ToDate)],
            typeof(CashFlowReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    // ---- The endpoint ------------------------------------------------------------

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new CashFlowReportController(null!)
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
            new CashFlowReportRequest { FromDate = From, ToDate = To, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_has_its_address_and_needs_reports_and_accounting_view()
    {
        Type controller = typeof(CashFlowReportController);

        // The gateway routes this exact path to Reporting; the page calls it.
        Assert.Equal("api/statements/cash-flow", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(CashFlowReportController.Run))!
                .GetCustomAttributes<RequirePermissionAttribute>()
                .Select(a => a.Permission));
    }
}
