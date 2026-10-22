using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The catalog's General Ledger Summary (TK-216), which export, saved views and
/// scheduled emails run on. It answered 500 on any branch with postings, because
/// each account's opening, debits and credits were sub-queries EF cannot translate;
/// it now sums the ledger by account once and joins two queries end to end. It takes
/// a period as the page does (both dates required, D-67), and opens an income or
/// expense account at what was posted since its financial year began, as the General
/// ledger page and the Trial Balance do.
///
/// The rows run on lists through the same expression the source runs against the
/// read models. The books are the Trial Balance's: an October 2026 period, a year
/// that began on 1 April 2026, and earlier years' sales of 3,000 and rent of 1,000.
/// </summary>
public sealed class GeneralLedgerSummarySourceTests
{
    private const long Bank = 1, Capital = 2, Sales = 3, Rent = 4, Idle = 5, Closed = 6;

    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 10, 31);
    private static readonly DateOnly YearStart = new(2026, 4, 1);

    private static AccountRead Account(long id, int type, string name, bool active = true) =>
        new() { AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = name, IsActive = active };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Bank, 1, "Bank"),
        Account(Capital, 3, "Capital"),
        Account(Sales, 4, "Sales"),
        Account(Rent, 5, "Rent"),
        Account(Idle, 1, "Petty Cash"),
        Account(Closed, 5, "Old Stationery", active: false),
    ];

    private static long _id;

    private static JournalLedgerRead Leg(DateOnly on, long account, decimal debit, decimal credit) =>
        new() { LedgerId = ++_id, LedgerDate = on, AccountId = account, DebitAmountBase = debit, CreditAmountBase = credit };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // Before the financial year: capital paid in, earlier years' sales and rent.
        Leg(new(2026, 1, 10), Bank, 10_000m, 0m), Leg(new(2026, 1, 10), Capital, 0m, 10_000m),
        Leg(new(2026, 2, 20), Bank, 3_000m, 0m), Leg(new(2026, 2, 20), Sales, 0m, 3_000m),
        Leg(new(2026, 2, 15), Rent, 1_000m, 0m), Leg(new(2026, 2, 15), Bank, 0m, 1_000m),

        // April to September.
        Leg(new(2026, 6, 10), Bank, 5_000m, 0m), Leg(new(2026, 6, 10), Sales, 0m, 5_000m),
        Leg(new(2026, 7, 5), Rent, 2_000m, 0m), Leg(new(2026, 7, 5), Bank, 0m, 2_000m),

        // October.
        Leg(new(2026, 10, 3), Bank, 7_000m, 0m), Leg(new(2026, 10, 3), Sales, 0m, 7_000m),
        Leg(new(2026, 10, 20), Rent, 2_500m, 0m), Leg(new(2026, 10, 20), Bank, 0m, 2_500m),

        // November, after the period.
        Leg(new(2026, 11, 5), Bank, 9_000m, 0m), Leg(new(2026, 11, 5), Sales, 0m, 9_000m),

        // An inactive account's posting in October.
        Leg(new(2026, 10, 10), Closed, 100m, 0m), Leg(new(2026, 10, 10), Bank, 0m, 100m),
    ];

    private static List<GeneralLedgerSummaryRow> Rows() =>
        [.. GeneralLedgerSummarySourceRepository.Rows(Accounts.AsQueryable(), Ledger.AsQueryable(), Start, End, YearStart).OrderBy(r => r.AccountId)];

    [Fact]
    public void A_balance_sheet_account_opens_at_everything_before_the_period()
    {
        GeneralLedgerSummaryRow bank = Rows().Single(r => r.AccountId == Bank);

        // 10,000 + 3,000 - 1,000 + 5,000 - 2,000 = 15,000; October 7,000 in and 2,500 + 100 out; November not in it.
        Assert.Equal((15_000m, 7_000m, 2_600m, 4_400m, 19_400m),
            (bank.OpeningBalance, bank.Debit, bank.Credit, bank.NetMovement, bank.Closing));

        // Equity opens at what was posted before, whenever.
        GeneralLedgerSummaryRow capital = Rows().Single(r => r.AccountId == Capital);
        Assert.Equal((-10_000m, 0m, 0m, -10_000m), (capital.OpeningBalance, capital.Debit, capital.Credit, capital.Closing));
    }

    [Fact]
    public void An_income_or_expense_account_opens_at_what_was_posted_since_the_financial_year_began()
    {
        // Sales: 5,000 credit since 1 April (the 3,000 of 2025-26 is earlier years' profit), 7,000 in October.
        GeneralLedgerSummaryRow sales = Rows().Single(r => r.AccountId == Sales);
        Assert.Equal((-5_000m, 0m, 7_000m, -12_000m), (sales.OpeningBalance, sales.Debit, sales.Credit, sales.Closing));

        // Rent: 2,000 since 1 April, 2,500 in October.
        GeneralLedgerSummaryRow rent = Rows().Single(r => r.AccountId == Rent);
        Assert.Equal((2_000m, 2_500m, 0m, 4_500m), (rent.OpeningBalance, rent.Debit, rent.Credit, rent.Closing));
    }

    [Fact]
    public void An_account_with_no_postings_is_listed_at_nothing_and_a_closed_one_is_not_listed()
    {
        List<GeneralLedgerSummaryRow> rows = Rows();

        GeneralLedgerSummaryRow idle = rows.Single(r => r.AccountId == Idle);
        Assert.Equal((0m, 0m, 0m, 0m, 0m), (idle.OpeningBalance, idle.Debit, idle.Credit, idle.NetMovement, idle.Closing));

        // The source lists the chart's active accounts, as it always did.
        Assert.DoesNotContain(rows, r => r.AccountId == Closed);
    }

    [Fact]
    public void An_account_with_postings_only_before_the_period_is_listed_with_its_opening_balance()
    {
        // Capital has nothing in October, and still opens and closes at 10,000.
        GeneralLedgerSummaryRow capital = Rows().Single(r => r.AccountId == Capital);

        Assert.Equal(capital.OpeningBalance, capital.Closing);
    }

    [Fact]
    public void Closing_is_opening_plus_the_movement_on_every_row()
    {
        Assert.All(Rows(), r => Assert.Equal(r.OpeningBalance + r.NetMovement, r.Closing));
        Assert.All(Rows(), r => Assert.Equal(r.Debit - r.Credit, r.NetMovement));
    }

    [Fact]
    public void A_period_beginning_with_the_financial_year_opens_income_and_expense_accounts_at_nothing()
    {
        List<GeneralLedgerSummaryRow> rows =
            [.. GeneralLedgerSummarySourceRepository.Rows(Accounts.AsQueryable(), Ledger.AsQueryable(), YearStart, new DateOnly(2026, 9, 30), YearStart)];

        Assert.Equal(0m, rows.Single(r => r.AccountId == Sales).OpeningBalance);
        Assert.Equal(0m, rows.Single(r => r.AccountId == Rent).OpeningBalance);
        Assert.Equal(12_000m, rows.Single(r => r.AccountId == Bank).OpeningBalance);
    }

    [Fact]
    public void Both_dates_are_declared_required()
    {
        var source = new GeneralLedgerSummarySourceRepository(new StubFinancialYear());

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
    }
}
