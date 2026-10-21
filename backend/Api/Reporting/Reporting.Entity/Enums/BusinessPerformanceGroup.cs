namespace Reporting.Entity.Enums;

/// <summary>The headings the Business Performance page groups its ratios under, in order (TK-203).</summary>
public enum BusinessPerformanceGroup
{
    /// <summary>Gross and net margins, and return on investment.</summary>
    Profitability = 1,

    /// <summary>How long customers take to pay, and how long the branch takes to pay suppliers.</summary>
    Efficiency = 2,

    /// <summary>Current and term assets against liabilities.</summary>
    FinancialPosition = 3,

    /// <summary>The cash and bank balance.</summary>
    Cash = 4,
}
