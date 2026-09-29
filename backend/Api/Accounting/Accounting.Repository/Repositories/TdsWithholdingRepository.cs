namespace Accounting.Repository.Repositories;

/// <summary>
/// How withheld TDS comes off a money document's bank legs (TK-117). Pure.
///
/// A money document posts one control leg and one bank leg per line. When
/// TDS is withheld the lines keep their gross — that is what the payment
/// settles or advances — and less money moves through the bank, so the
/// withheld amount is taken off the bank legs of the lines that bear it, in
/// line order, and posted to TDS Payable or TDS Receivable instead.
/// </summary>
public static class TdsWithholdingRepository
{
    /// <summary>
    /// The bank amount for each line. Lines that do not bear TDS keep their
    /// whole amount; the withheld amount is taken from the bearing lines in
    /// order, each down to zero at most.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">More is withheld than the bearing lines carry.</exception>
    public static IReadOnlyDictionary<int, decimal> BankShares(
        IEnumerable<(int LineNumber, decimal Amount, bool Bears)> lines, decimal withheld)
    {
        Dictionary<int, decimal> shares = [];
        decimal left = withheld;

        foreach ((int number, decimal amount, bool bears) in lines.OrderBy(l => l.LineNumber))
        {
            decimal taken = bears ? Math.Min(amount, left) : 0m;
            left -= taken;
            shares[number] = amount - taken;
        }

        if (left > 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(withheld), withheld, "More TDS is withheld than the lines that bear it carry.");
        }

        return shares;
    }

    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);
}
