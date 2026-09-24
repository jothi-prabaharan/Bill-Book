namespace Reporting.Entity.Enums;

/// <summary>
/// What a Business Performance value is measured in. The ratios are not one
/// kind of number, so the unit travels with each row rather than with the
/// column.
/// </summary>
public enum BusinessPerformanceUnit
{
    /// <summary>A percentage: 25 is twenty-five percent.</summary>
    Percent = 1,

    Days = 2,

    /// <summary>One amount divided by another: 1.5 is one and a half times.</summary>
    Times = 3,

    /// <summary>A sum of money in the branch's base currency.</summary>
    Amount = 4,
}
