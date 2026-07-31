namespace Shared.Kernel.Numbering;

/// <summary>
/// How the financial year is rendered inside a generated number. All four are
/// in common use on Indian documents, so the choice is per series rather than
/// a constant.
/// </summary>
public enum FinancialYearFormat
{
    /// <summary>2025-26</summary>
    FullYearRange = 0,

    /// <summary>25-26</summary>
    ShortYearRange = 1,

    /// <summary>2526</summary>
    Compact = 2,

    /// <summary>2025</summary>
    StartYear = 3,
}
