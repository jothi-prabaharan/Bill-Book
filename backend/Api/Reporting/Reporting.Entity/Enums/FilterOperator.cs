namespace Reporting.Entity.Enums;

/// <summary>
/// How a filter compares. Which of these a column offers follows its
/// <see cref="ColumnDataType"/> — text gets the string operators, numbers and
/// dates get the comparisons and <see cref="Between"/>, enums get
/// <see cref="In"/>.
/// </summary>
public enum FilterOperator
{
    Equals = 1,
    NotEquals = 2,

    Contains = 3,
    NotContains = 4,
    StartsWith = 5,
    EndsWith = 6,

    GreaterThan = 7,
    GreaterOrEqual = 8,
    LessThan = 9,
    LessOrEqual = 10,

    /// <summary>Inclusive at both ends, and needs both values.</summary>
    Between = 11,

    In = 12,
    NotIn = 13,

    IsNull = 14,
    IsNotNull = 15,
}
