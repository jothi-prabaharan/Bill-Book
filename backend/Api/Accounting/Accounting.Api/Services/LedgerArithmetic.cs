using Accounting.Entity.Models;

namespace Accounting.Api.Services;

/// <summary>
/// The arithmetic behind the two ledger reads, pulled out of the queries that
/// feed it.
///
/// Pure and static because this is the part that is wrong quietly. A running
/// balance that starts from the wrong opening figure is wrong by the same amount
/// all the way down the page and looks entirely plausible; a trial balance that
/// puts a net on the wrong side still foots, because it moved the same number
/// from one column to the other. Neither fails. Both are only caught by being
/// tested against numbers someone worked out by hand.
/// </summary>
public static class LedgerArithmetic
{
    /// <summary>
    /// An account's position in <b>debit terms</b>: positive is a debit balance,
    /// negative a credit one.
    ///
    /// One convention, used everywhere, rather than each report deciding which
    /// way up an account sits. Which way up it should be <i>displayed</i> depends
    /// on the account's normal balance, and that lives in the master database —
    /// so it is the screen's business, not this one's.
    /// </summary>
    public static decimal Net(decimal debit, decimal credit) => debit - credit;

    /// <summary>
    /// Which trial-balance column a net falls in. Exactly one of the two is
    /// non-zero, and a zero net is in neither.
    ///
    /// Both are returned positive: a trial balance foots two columns of positive
    /// numbers and compares them, and a credit written as a negative debit would
    /// make the two totals agree at zero no matter what the books said.
    /// </summary>
    public static (decimal Debit, decimal Credit) Sides(decimal net) =>
        net > 0 ? (net, 0m) : net < 0 ? (0m, -net) : (0m, 0m);

    /// <summary>
    /// Stamps each row with the balance after it, carrying on from what the
    /// account stood at when the range opened.
    ///
    /// Returns the closing balance. Rows must already be in posting order —
    /// date then id — because a running balance over rows in any other order is
    /// a column of numbers that means nothing.
    /// </summary>
    public static decimal ApplyRunningBalance(
        IEnumerable<AccountLedgerRow> rows, decimal opening)
    {
        decimal running = opening;

        foreach (AccountLedgerRow row in rows)
        {
            running += Net(row.DebitAmountBase, row.CreditAmountBase);
            row.RunningBalance = running;
        }

        return running;
    }
}
