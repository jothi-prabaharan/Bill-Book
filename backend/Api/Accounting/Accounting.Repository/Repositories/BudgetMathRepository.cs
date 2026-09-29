using System.Globalization;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The arithmetic and the spreadsheet layout behind budgets (TK-108), kept pure
/// so they can be tested without a database.
/// </summary>
public static class BudgetMathRepository
{
    public const int Months = 12;

    /// <summary>Asset and expense accounts run debit; liability, equity and income run credit.</summary>
    public static bool IsDebitNormal(int accountTypeId, bool isContra) =>
        (accountTypeId is 1 or 5) != isContra;

    /// <summary>
    /// A ledger movement expressed in the account's normal direction, the way a
    /// budget is entered: rent paid is positive, sales earned are positive, and
    /// a sales return (a contra income account) is positive too.
    /// </summary>
    public static decimal NormalAmount(int accountTypeId, bool isContra, decimal debit, decimal credit) =>
        IsDebitNormal(accountTypeId, isContra) ? debit - credit : credit - debit;

    /// <summary>1 to 12 for a date inside the financial year starting <paramref name="yearStart"/>; 0 outside it.</summary>
    public static int PeriodOf(DateOnly yearStart, DateOnly date)
    {
        int months = ((date.Year - yearStart.Year) * 12) + date.Month - yearStart.Month;
        return months is >= 0 and < Months ? months + 1 : 0;
    }

    /// <summary>The first day of the financial year holding <paramref name="date"/>.</summary>
    public static DateOnly YearStartOf(DateOnly date, int startMonth)
    {
        int year = date.Month >= startMonth ? date.Year : date.Year - 1;
        return new DateOnly(year, startMonth, 1);
    }

    /// <summary>
    /// Amounts adjusted by a percentage and rounded to the paisa. A figure that
    /// came out negative (a month where refunds outran spending) starts the
    /// budget at zero, because a budget amount is never negative.
    /// </summary>
    public static decimal[] Adjust(IReadOnlyList<decimal> amounts, decimal percent) =>
        [.. amounts.Select(a => Math.Max(0m, Math.Round(a * (1m + (percent / 100m)), 2, MidpointRounding.AwayFromZero)))];

    /// <summary>The adjustment for an account type: its own when given, the general one otherwise.</summary>
    public static decimal PercentFor(int accountTypeId, decimal general, IReadOnlyDictionary<int, decimal> byType) =>
        byType.TryGetValue(accountTypeId, out decimal own) ? own : general;

    /// <summary>The month headings for a year: "Apr 2026" to "Mar 2027".</summary>
    public static string[] MonthHeadings(DateOnly yearStart) =>
        [.. Enumerable.Range(0, Months).Select(i => yearStart.AddMonths(i).ToString("MMM yyyy", CultureInfo.InvariantCulture))];

    /// <summary>
    /// The export and import layout: account code, the project code on a budget
    /// by project, the account name, twelve months and a total. The name and the
    /// total are for the reader and are ignored on the way back in.
    /// </summary>
    public static string[] SheetHeader(DateOnly yearStart, bool byProject) =>
        [.. (byProject ? new[] { "Account code", "Project code", "Account name" } : ["Account code", "Account name"]),
            .. MonthHeadings(yearStart),
            "Total"];

    /// <summary>
    /// Reads the rows of an imported sheet. Codes are returned as written, for
    /// the caller to resolve; an amount that is not a number refuses the whole
    /// file with its row and column, because a budget half-loaded from a
    /// spreadsheet is worse than one not loaded at all.
    /// </summary>
    public static SheetParse ParseSheet(IReadOnlyList<string[]> rows, bool byProject)
    {
        int firstMonth = byProject ? 3 : 2;
        List<SheetRow> parsed = [];

        for (int r = 0; r < rows.Count; r++)
        {
            string[] cells = rows[r];
            string code = Cell(cells, 0);

            if (code.Length == 0)
            {
                // A blank line, or a totals row a spreadsheet added at the bottom.
                continue;
            }

            string? project = byProject ? NullIfBlank(Cell(cells, 1)) : null;
            decimal[] amounts = new decimal[Months];

            for (int m = 0; m < Months; m++)
            {
                string raw = Cell(cells, firstMonth + m).Replace(",", string.Empty, StringComparison.Ordinal);

                if (raw.Length == 0)
                {
                    continue;
                }

                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
                {
                    return new SheetParse([], $"Row {r + 2}, month {m + 1}: \"{Cell(cells, firstMonth + m)}\" is not a number.");
                }

                if (amount < 0m)
                {
                    return new SheetParse([], $"Row {r + 2}, month {m + 1}: a budget amount cannot be negative.");
                }

                amounts[m] = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            }

            parsed.Add(new SheetRow(code.Trim(), project?.Trim(), amounts));
        }

        return new SheetParse(parsed, null);
    }

    private static string Cell(string[] cells, int index) =>
        index < cells.Length ? (cells[index] ?? string.Empty).Trim() : string.Empty;

    private static string? NullIfBlank(string value) => value.Length == 0 ? null : value;
}

/// <summary>One imported row: codes as written, and twelve amounts.</summary>
public sealed record SheetRow(string AccountCode, string? ProjectCode, decimal[] Amounts);

/// <summary>The rows read, or why the file was refused.</summary>
public sealed record SheetParse(IReadOnlyList<SheetRow> Rows, string? Error);
