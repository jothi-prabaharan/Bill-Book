using Accounting.Api.Services;
using Accounting.Entity.Models;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The two calculations behind the ledger screens. Both fail silently when
/// wrong — a running balance off by its opening figure looks entirely plausible
/// all the way down the page, and a net put in the wrong trial-balance column
/// still foots — so both are checked against figures worked out by hand.
/// </summary>
public class LedgerArithmeticTests
{
    [Theory]
    [InlineData(500, 0, 500)]
    [InlineData(0, 500, -500)]
    [InlineData(500, 500, 0)]
    [InlineData(1200.50, 300.25, 900.25)]
    public void Net_is_debits_less_credits(decimal debit, decimal credit, decimal expected) =>
        Assert.Equal(expected, LedgerArithmetic.Net(debit, credit));

    [Fact]
    public void A_debit_net_lands_in_the_debit_column_only()
    {
        (decimal debit, decimal credit) = LedgerArithmetic.Sides(750m);

        Assert.Equal(750m, debit);
        Assert.Equal(0m, credit);
    }

    /// <summary>
    /// The one that would be easy to get wrong: a credit balance is a negative
    /// net, and it goes into the credit column <b>positive</b>. Left negative,
    /// the two columns would foot to the same number no matter what the books
    /// said, and the trial balance would agree with itself forever.
    /// </summary>
    [Fact]
    public void A_credit_net_lands_in_the_credit_column_as_a_positive()
    {
        (decimal debit, decimal credit) = LedgerArithmetic.Sides(-750m);

        Assert.Equal(0m, debit);
        Assert.Equal(750m, credit);
    }

    [Fact]
    public void A_zero_net_is_in_neither_column()
    {
        (decimal debit, decimal credit) = LedgerArithmetic.Sides(0m);

        Assert.Equal(0m, debit);
        Assert.Equal(0m, credit);
    }

    [Fact]
    public void The_running_balance_accumulates_from_the_opening_figure()
    {
        List<AccountLedgerRow> rows =
        [
            Row(debit: 100m),
            Row(credit: 40m),
            Row(debit: 15m),
        ];

        decimal closing = LedgerArithmetic.ApplyRunningBalance(rows, opening: 250m);

        Assert.Equal(350m, rows[0].RunningBalance);
        Assert.Equal(310m, rows[1].RunningBalance);
        Assert.Equal(325m, rows[2].RunningBalance);
        Assert.Equal(325m, closing);
    }

    /// <summary>
    /// An account read from the start of the books opens at zero, and the first
    /// row's balance is that row alone.
    /// </summary>
    [Fact]
    public void With_no_opening_balance_the_first_row_stands_alone()
    {
        List<AccountLedgerRow> rows = [Row(credit: 90m)];

        Assert.Equal(-90m, LedgerArithmetic.ApplyRunningBalance(rows, opening: 0m));
        Assert.Equal(-90m, rows[0].RunningBalance);
    }

    [Fact]
    public void An_empty_ledger_closes_where_it_opened() =>
        Assert.Equal(120m, LedgerArithmetic.ApplyRunningBalance([], opening: 120m));

    private static AccountLedgerRow Row(decimal debit = 0m, decimal credit = 0m) => new()
    {
        TransactionTypeCode = "JRN",
        CurrencyCode = "INR",
        DebitAmountBase = debit,
        CreditAmountBase = credit,
    };
}
