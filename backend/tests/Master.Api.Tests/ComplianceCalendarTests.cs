using Master.Entity.TableEntities;
using Master.Repository.Services;
using Xunit;

namespace Master.Api.Tests;

public class ComplianceCalendarTests
{
    [Fact]
    public void Calendar_Computes_Right_Date_Across_Month_Ends()
    {
        // Rule: 31st of the following month
        var rule31 = new ComplianceDueDate
        {
            FormName = "Test-Return",
            Category = "Tax",
            DueDay = 31,
            DueMonthOffset = 1,
            RuleDescription = "31st of following month",
        };

        // 1. January period -> Due in February (non-leap year 2025: 28 days)
        DateOnly dueFeb2025 = ComplianceCalendarCalculator.ComputeDueDate(rule31, 2025, 1);
        Assert.Equal(new DateOnly(2025, 2, 28), dueFeb2025);

        // 2. January period -> Due in February (leap year 2024: 29 days)
        DateOnly dueFeb2024 = ComplianceCalendarCalculator.ComputeDueDate(rule31, 2024, 1);
        Assert.Equal(new DateOnly(2024, 2, 29), dueFeb2024);

        // 3. March period -> Due in April (30 days)
        DateOnly dueApr2026 = ComplianceCalendarCalculator.ComputeDueDate(rule31, 2026, 3);
        Assert.Equal(new DateOnly(2026, 4, 30), dueApr2026);

        // 4. December period -> Due in January of next year (31 days)
        DateOnly dueJan2027 = ComplianceCalendarCalculator.ComputeDueDate(rule31, 2026, 12);
        Assert.Equal(new DateOnly(2027, 1, 31), dueJan2027);
    }

    [Fact]
    public void Gstr1_Due_Date_Is_11th_Of_Following_Month()
    {
        var gstr1 = new ComplianceDueDate
        {
            FormName = "GSTR-1",
            Category = "GST",
            DueDay = 11,
            DueMonthOffset = 1,
            RuleDescription = "11th of following month",
        };

        DateOnly dueSep = ComplianceCalendarCalculator.ComputeDueDate(gstr1, 2026, 8);
        Assert.Equal(new DateOnly(2026, 9, 11), dueSep);

        DateOnly dueJan = ComplianceCalendarCalculator.ComputeDueDate(gstr1, 2026, 12);
        Assert.Equal(new DateOnly(2027, 1, 11), dueJan);
    }

    [Fact]
    public void Tds_March_Deposit_Due_On_April_30()
    {
        var tds = new ComplianceDueDate
        {
            FormName = "TDS Deposit",
            Category = "TDS",
            DueDay = 7,
            DueMonthOffset = 1,
            RuleDescription = "7th of following month (30th April for March)",
        };

        // Regular month: August -> 7 September
        DateOnly dueRegular = ComplianceCalendarCalculator.ComputeDueDate(tds, 2026, 8);
        Assert.Equal(new DateOnly(2026, 9, 7), dueRegular);

        // March month: -> 30 April
        DateOnly dueMarch = ComplianceCalendarCalculator.ComputeDueDate(tds, 2026, 3);
        Assert.Equal(new DateOnly(2026, 4, 30), dueMarch);
    }

    [Fact]
    public void Form_26Q_Q4_Due_On_May_31()
    {
        var form26Q = new ComplianceDueDate
        {
            FormName = "Form 26Q",
            Category = "TDS",
            DueDay = 31,
            DueMonthOffset = 1,
            RuleDescription = "31st of month following quarter (31st May for Q4)",
        };

        // Q1 (Apr-Jun, ending month 6) -> Due 31 July
        DateOnly dueQ1 = ComplianceCalendarCalculator.ComputeDueDate(form26Q, 2026, 6);
        Assert.Equal(new DateOnly(2026, 7, 31), dueQ1);

        // Q4 (Jan-Mar, ending month 3) -> Special IT rule: Due 31 May
        DateOnly dueQ4 = ComplianceCalendarCalculator.ComputeDueDate(form26Q, 2026, 3);
        Assert.Equal(new DateOnly(2026, 5, 31), dueQ4);
    }

    [Fact]
    public void Operator_Extended_Due_Date_Overrides_Default_Rule()
    {
        var gstr3B = new ComplianceDueDate
        {
            FormName = "GSTR-3B",
            Category = "GST",
            DueDay = 20,
            DueMonthOffset = 1,
            RuleDescription = "20th of following month",
            SpecificPeriod = "2026-08",
            ExtendedDueDate = new DateOnly(2026, 9, 25), // Government extended from 20th to 25th
            ExtensionNotice = "Extended via Notification 45/2026",
        };

        // August 2026 period -> Extended date applies
        DateOnly dueAug = ComplianceCalendarCalculator.ComputeDueDate(gstr3B, 2026, 8);
        Assert.Equal(new DateOnly(2026, 9, 25), dueAug);

        // July 2026 period -> Standard date applies (20 August)
        DateOnly dueJul = ComplianceCalendarCalculator.ComputeDueDate(gstr3B, 2026, 7);
        Assert.Equal(new DateOnly(2026, 8, 20), dueJul);
    }
}
