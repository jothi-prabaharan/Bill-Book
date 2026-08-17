namespace Reporting.Entity.Enums;

/// <summary>
/// What a column holds. Three things read this and must agree: which filter
/// operators the UI offers, how the grid formats the value, and which number
/// format the Excel writer puts on the cell.
/// </summary>
public enum ColumnDataType
{
    Text = 1,

    /// <summary>A plain count or integer. Not money — see <see cref="Money"/>.</summary>
    Number = 2,

    /// <summary>
    /// An amount. Formatted to the branch's currency decimals, and the only type
    /// whose header may carry the <c>%CurCode%</c> substitution.
    /// </summary>
    Money = 3,

    /// <summary>A stock quantity, which carries more decimals than money does.</summary>
    Quantity = 4,

    /// <summary>A percentage, stored as the percentage rather than the fraction: 18 is 18%.</summary>
    Percent = 5,

    /// <summary>An exchange rate. Six decimals, and never rounded for display.</summary>
    Rate = 6,

    Date = 7,

    DateTime = 8,

    Boolean = 9,

    /// <summary>A fixed set of values, filtered by multi-select rather than by text.</summary>
    Enum = 10,

    /// <summary>
    /// A value that navigates somewhere — a transaction number opening its
    /// document. Rendered as a link by the grid; exported as its text by Excel.
    /// </summary>
    Link = 11,
}
