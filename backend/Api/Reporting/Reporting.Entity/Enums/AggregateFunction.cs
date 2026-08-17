namespace Reporting.Entity.Enums;

/// <summary>
/// What a group footer, a grand total or a pivot cell computes for a column.
/// <see cref="None"/> is the default and means the column has no meaningful
/// total — summing a rate or an account code produces a number that looks like
/// an answer and is not one.
/// </summary>
public enum AggregateFunction
{
    None = 0,
    Sum = 1,
    Count = 2,
    CountDistinct = 3,
    Min = 4,
    Max = 5,
    Avg = 6,
}
