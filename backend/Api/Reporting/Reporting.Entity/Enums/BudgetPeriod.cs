namespace Reporting.Entity.Enums;

/// <summary>
/// Which whole months of a budget's year a budget report covers, counted from
/// the as-at date's month (TK-204): that month, its financial quarter, or the
/// year up to the end of it.
/// </summary>
public enum BudgetPeriod
{
    Month = 1,
    Quarter = 2,
    YearToDate = 3,
}
