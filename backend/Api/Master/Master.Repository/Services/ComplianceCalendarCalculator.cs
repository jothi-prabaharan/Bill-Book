using Master.Entity.TableEntities;

namespace Master.Repository.Services;

/// <summary>
/// Computes statutory due dates according to Indian tax rules (TK-119),
/// clamping properly across month-ends (e.g. 28/29 Feb, 30-day months like April, etc.).
/// </summary>
public static class ComplianceCalendarCalculator
{
    /// <summary>
    /// Computes the statutory due date for a given rule and period, properly clamping
    /// to the month length (e.g. 28/29 Feb, 30-day months like April, etc.).
    /// </summary>
    public static DateOnly ComputeDueDate(ComplianceDueDate rule, int periodYear, int periodMonth)
    {
        // If an operator-extended due date applies to this exact period, that wins.
        string periodKey = $"{periodYear:D4}-{periodMonth:D2}";
        if (string.Equals(rule.SpecificPeriod, periodKey, StringComparison.OrdinalIgnoreCase) && rule.ExtendedDueDate.HasValue)
        {
            return rule.ExtendedDueDate.Value;
        }

        // Special statutory rule exceptions:
        // 1) TDS Deposit for March: Income-tax rule gives till April 30 rather than April 7.
        if (rule.FormName == "TDS Deposit" && periodMonth == 3)
        {
            return new DateOnly(periodYear, 4, 30);
        }

        // 2) Form 26Q / 27Q for Q4 (Jan-Mar): Income-tax department sets due date to May 31 (2 months offset) rather than April 30.
        if ((rule.FormName == "Form 26Q" || rule.FormName == "Form 27Q") && periodMonth == 3)
        {
            return new DateOnly(periodYear, 5, 31);
        }

        // 3) Advance Tax quarters:
        // Q1 (Apr-Jun) -> 15 June
        // Q2 (Jul-Sep) -> 15 September
        // Q3 (Oct-Dec) -> 15 December
        // Q4 (Jan-Mar) -> 15 March
        if (rule.FormName == "Advance Tax")
        {
            return periodMonth switch
            {
                6 => new DateOnly(periodYear, 6, 15),
                9 => new DateOnly(periodYear, 9, 15),
                12 => new DateOnly(periodYear, 12, 15),
                3 => new DateOnly(periodYear, 3, 15),
                _ => new DateOnly(periodYear, periodMonth, Math.Min(15, DateTime.DaysInMonth(periodYear, periodMonth)))
            };
        }

        // 4) MSME Form 1 (half-yearly):
        // Period 1 (Apr - Sep, end = month 9): Due 31 October
        // Period 2 (Oct - Mar, end = month 3): Due 30 April
        if (rule.FormName == "MSME Form 1")
        {
            return periodMonth switch
            {
                9 => new DateOnly(periodYear, 10, 31),
                3 => new DateOnly(periodYear, 4, 30),
                _ => new DateOnly(periodYear, periodMonth, Math.Min(30, DateTime.DaysInMonth(periodYear, periodMonth)))
            };
        }

        // Standard offset calculation:
        int targetMonth = periodMonth + rule.DueMonthOffset;
        int targetYear = periodYear;
        while (targetMonth > 12)
        {
            targetMonth -= 12;
            targetYear++;
        }

        int daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        int targetDay = Math.Min(rule.DueDay, daysInTargetMonth);

        return new DateOnly(targetYear, targetMonth, targetDay);
    }
}
