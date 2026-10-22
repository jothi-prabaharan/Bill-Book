using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The catalog's Trial Balance (TK-215), which export, saved views and scheduled
/// emails run on. It answered 500 on any branch with postings, because each
/// account's debits and credits were two sub-queries EF cannot translate; it now
/// sums the ledger by account once and joins the accounts to the sums. It also
/// takes a period as the page does: both dates are required (D-67).
///
/// The rows run on lists through the same expression the source runs against the
/// read models. The period rule is pure.
/// </summary>
public sealed class TrialBalanceSourceTests
{
    private const long Bank = 1, Sales = 2, Rent = 3, Idle = 4, Closed = 5;

    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 10, 31);

    private static AccountRead Account(long id, int type, string name, bool active = true) =>
        new() { AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = name, IsActive = active };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Bank, 1, "Bank"),
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
        // Before the period.
        Leg(new(2026, 9, 30), Bank, 5_000m, 0m),
        Leg(new(2026, 9, 30), Sales, 0m, 5_000m),

        // In the period: 7,000 of sales, then 2,500 of rent.
        Leg(new(2026, 10, 3), Bank, 7_000m, 0m),
        Leg(new(2026, 10, 3), Sales, 0m, 7_000m),
        Leg(new(2026, 10, 31), Rent, 2_500m, 0m),
        Leg(new(2026, 10, 31), Bank, 0m, 2_500m),

        // After it.
        Leg(new(2026, 11, 1), Bank, 9_000m, 0m),
        Leg(new(2026, 11, 1), Sales, 0m, 9_000m),

        // An inactive account's posting in the period.
        Leg(new(2026, 10, 10), Closed, 100m, 0m),
        Leg(new(2026, 10, 10), Bank, 0m, 100m),
    ];

    private static List<TrialBalanceRow> Rows() =>
        [.. TrialBalanceSourceRepository.Rows(Accounts.AsQueryable(), Ledger.AsQueryable(), Start, End).OrderBy(r => r.AccountId)];

    [Fact]
    public void Each_account_has_its_debits_and_credits_for_the_period_alone()
    {
        List<TrialBalanceRow> rows = Rows();

        // Bank: 7,000 in, 2,500 + 100 out; the 5,000 of September and 9,000 of November are not in it.
        TrialBalanceRow bank = rows.Single(r => r.AccountId == Bank);
        Assert.Equal((7_000m, 2_600m, 4_400m), (bank.Debit, bank.Credit, bank.CurrentBalance));

        TrialBalanceRow sales = rows.Single(r => r.AccountId == Sales);
        Assert.Equal((0m, 7_000m, -7_000m), (sales.Debit, sales.Credit, sales.CurrentBalance));

        TrialBalanceRow rent = rows.Single(r => r.AccountId == Rent);
        Assert.Equal((2_500m, 0m, 2_500m), (rent.Debit, rent.Credit, rent.CurrentBalance));
    }

    [Fact]
    public void An_account_with_no_postings_is_listed_at_nothing_and_a_closed_one_is_not_listed()
    {
        List<TrialBalanceRow> rows = Rows();

        TrialBalanceRow idle = rows.Single(r => r.AccountId == Idle);
        Assert.Equal((0m, 0m, 0m), (idle.Debit, idle.Credit, idle.CurrentBalance));

        // The source lists the chart's active accounts, as it always did.
        Assert.DoesNotContain(rows, r => r.AccountId == Closed);
    }

    [Fact]
    public void The_account_carries_its_type_for_the_name_to_be_filled_in_afterwards()
    {
        Assert.Equal([1, 4, 5, 1], Rows().Select(r => r.AccountTypeId));
    }

    // ---- The period -------------------------------------------------------------------

    private static ReportParameters Parameters(string? from, string? to) =>
        new(new Dictionary<string, string?> { ["from"] = from, ["to"] = to });

    [Fact]
    public void Both_dates_are_declared_required()
    {
        var source = new TrialBalanceSourceRepository(
            new BatchedNameResolverRepository(
                new HttpClient { BaseAddress = new Uri("http://reporting.tests.invalid") },
                new MemoryCache(new MemoryCacheOptions())));

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
    }

    [Theory]
    [InlineData(null, "2026-10-31", "From date is required.")]
    [InlineData("2026-10-01", null, "To date is required.")]
    [InlineData("2026-10-31", "2026-10-01", "To date cannot be before the from date.")]
    public void A_run_without_a_period_is_refused_with_the_reason(string? from, string? to, string message)
    {
        ReportQueryException refusal = Assert.Throws<ReportQueryException>(
            () => TrialBalanceSourceRepository.PeriodOf(Parameters(from, to)));

        Assert.Equal(message, refusal.Message);
    }

    [Fact]
    public void A_period_is_taken_as_given_and_one_day_is_a_period()
    {
        Assert.Equal((Start, End), TrialBalanceSourceRepository.PeriodOf(Parameters("2026-10-01", "2026-10-31")));
        Assert.Equal((Start, Start), TrialBalanceSourceRepository.PeriodOf(Parameters("2026-10-01", "2026-10-01")));
    }
}
