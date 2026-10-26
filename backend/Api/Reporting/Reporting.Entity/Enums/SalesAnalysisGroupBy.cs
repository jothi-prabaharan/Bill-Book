namespace Reporting.Entity.Enums;

/// <summary>What the Sales Analysis (TK-228) adds sales up by.</summary>
public enum SalesAnalysisGroupBy
{
    /// <summary>One row for each item sold; lines with no item are one row.</summary>
    Item = 1,

    /// <summary>One row for each item category; items with none, and lines with no item, are one row.</summary>
    Category = 2,

    /// <summary>One row for each customer.</summary>
    Customer = 3,
}
