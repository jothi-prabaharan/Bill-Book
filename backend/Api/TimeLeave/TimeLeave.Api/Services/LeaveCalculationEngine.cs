using TimeLeave.Entity.Enums;
using TimeLeave.Entity.TableEntities;

namespace TimeLeave.Api.Services;

public class LeaveCalculationEngine
{
    /// <summary>
    /// Calculates the number of leave days between fromDate and toDate.
    /// If sandwich rule is true, intervening holidays and weekly offs count as leave.
    /// If sandwich rule is false, intervening holidays and weekly offs are excluded.
    /// </summary>
    public decimal CalculateDays(
        DateOnly fromDate,
        DateOnly toDate,
        LeaveHalf fromHalf,
        LeaveHalf toHalf,
        LeavePolicy policy,
        HashSet<DateOnly> holidays,
        WeeklyOffPolicy weeklyOff)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("ToDate must be greater than or equal to FromDate.");
        }

        if (fromDate == toDate)
        {
            // Single day
            if (IsNonWorkingDay(fromDate, holidays, weeklyOff))
            {
                return 0m;
            }

            return (fromHalf == LeaveHalf.Full && toHalf == LeaveHalf.Full) ? 1.0m : 0.5m;
        }

        decimal totalDays = 0m;

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            bool isNonWorking = IsNonWorkingDay(date, holidays, weeklyOff);

            if (isNonWorking)
            {
                if (policy.IsSandwichRule)
                {
                    // Intervening weekly offs and holidays count as full leave days under sandwich rule
                    totalDays += 1.0m;
                }
                // else non-working day is skipped / 0 days
            }
            else
            {
                decimal dayVal = 1.0m;
                if (date == fromDate && fromHalf != LeaveHalf.Full)
                {
                    dayVal = 0.5m;
                }
                else if (date == toDate && toHalf != LeaveHalf.Full)
                {
                    dayVal = 0.5m;
                }

                totalDays += dayVal;
            }
        }

        return totalDays;
    }

    public bool IsNonWorkingDay(DateOnly date, HashSet<DateOnly> holidays, WeeklyOffPolicy weeklyOff)
    {
        if (holidays.Contains(date))
        {
            return true;
        }

        DayOfWeek dow = date.DayOfWeek;
        WeeklyOffKind rule = dow switch
        {
            DayOfWeek.Monday => weeklyOff.MondayRule,
            DayOfWeek.Tuesday => weeklyOff.TuesdayRule,
            DayOfWeek.Wednesday => weeklyOff.WednesdayRule,
            DayOfWeek.Thursday => weeklyOff.ThursdayRule,
            DayOfWeek.Friday => weeklyOff.FridayRule,
            DayOfWeek.Saturday => weeklyOff.SaturdayRule,
            DayOfWeek.Sunday => weeklyOff.SundayRule,
            _ => WeeklyOffKind.Working
        };

        if (rule == WeeklyOffKind.Off)
        {
            return true;
        }

        if (rule == WeeklyOffKind.AlternateOff)
        {
            // Check alternate weeks e.g. "2,4"
            int weekOfMonth = ((date.Day - 1) / 7) + 1;
            if (!string.IsNullOrWhiteSpace(weeklyOff.AlternateWeeks))
            {
                var weeks = weeklyOff.AlternateWeeks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (weeks.Contains(weekOfMonth.ToString()))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
