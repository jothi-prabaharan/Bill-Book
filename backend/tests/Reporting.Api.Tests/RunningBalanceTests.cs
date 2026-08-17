using Reporting.Api.Services;
using Reporting.Api.Services.Sources;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// Account Transaction's running balance.
///
/// <b>The failure this guards against is the quiet one.</b> A running balance that
/// starts each page from zero still adds up perfectly within the page — every row
/// is the one above plus this row's movement — so nothing looks wrong. It is only
/// wrong by the whole of every preceding page, which is exactly the amount nobody
/// notices until they reconcile.
/// </summary>
public class RunningBalanceTests
{
    private readonly AccountTransactionSource _report = new();

    private static AccountTransactionRow Leg(long id, decimal debit, decimal credit) =>
        new()
        {
            LedgerId = id,
            Date = new DateOnly(2026, 4, (int)id),
            TransactionNo = $"JRN-{id}",
            Source = "JRN",
            AccountCode = "1100",
            Account = "Cash",
            Currency = "INR",
            ExchangeRate = 1m,
            Debit = debit,
            Credit = credit,
        };

    /// <summary>The protected hook, reached the way the engine reaches it.</summary>
    private void Apply(IReadOnlyList<AccountTransactionRow> page, decimal opening) =>
        typeof(ReportSource<AccountTransactionRow>)
            .GetMethod("ApplyRunningTotals", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(_report, [page, opening]);

    [Fact]
    public void The_balance_carries_down_the_page()
    {
        List<AccountTransactionRow> page = [Leg(1, 100m, 0m), Leg(2, 0m, 30m), Leg(3, 50m, 0m)];

        Apply(page, 0m);

        Assert.Equal(100m, page[0].RunningBalance);
        Assert.Equal(70m, page[1].RunningBalance);
        Assert.Equal(120m, page[2].RunningBalance);
    }

    [Fact]
    public void A_later_page_starts_from_what_came_before_it()
    {
        // The whole point. Page two opening at zero would still add up internally
        // and be wrong by the whole of page one.
        List<AccountTransactionRow> page = [Leg(4, 25m, 0m), Leg(5, 0m, 5m)];

        Apply(page, 120m);

        Assert.Equal(145m, page[0].RunningBalance);
        Assert.Equal(140m, page[1].RunningBalance);
    }

    [Fact]
    public void Credits_take_the_balance_negative_rather_than_flipping_its_sign()
    {
        // A credit account simply runs negative and the report says so. Flipping
        // the sign per account type would leave the reader guessing which way a
        // given row went.
        List<AccountTransactionRow> page = [Leg(1, 0m, 400m), Leg(2, 150m, 0m)];

        Apply(page, 0m);

        Assert.Equal(-400m, page[0].RunningBalance);
        Assert.Equal(-250m, page[1].RunningBalance);
    }

    [Fact]
    public void The_report_forces_its_own_order()
    {
        // Because the balance is only true of one ordering. The engine refuses a
        // sort request outright, and the grid disables the other columns.
        Assert.True(_report.ForcesSortOrder);
    }

    [Fact]
    public void The_running_balance_column_is_neither_totalled_nor_sorted_nor_filtered()
    {
        ReportColumn balance = _report.Columns.Single(c => c.Key == "runningBalance");

        // Totalling it would sum every intermediate balance — a number with no
        // meaning that looks like a closing figure.
        Assert.False(balance.IsAggregatable);
        Assert.False(balance.IsSortable);
        Assert.False(balance.IsFilterable);
    }

    [Fact]
    public void Both_currencies_are_offered_and_only_the_base_one_is_totalled()
    {
        // A (Source) column is meaningless without Currency and ExchangeRate
        // beside it, and totalling one would foot a rupee row and a dollar row
        // into a number in no currency at all.
        Assert.Contains(_report.Columns, c => c.Key == "currency");
        Assert.Contains(_report.Columns, c => c.Key == "exchangeRate");

        Assert.Contains(_report.Columns, c => c.Key == "debitSource");
        Assert.Contains(_report.Columns, c => c.Key == "debit");
    }
}
