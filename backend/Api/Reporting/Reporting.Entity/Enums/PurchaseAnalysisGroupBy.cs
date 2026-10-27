namespace Reporting.Entity.Enums;

/// <summary>What the Purchase Analysis (TK-239) adds purchases up by.</summary>
public enum PurchaseAnalysisGroupBy
{
    /// <summary>One row for each item bought; lines with no item (a service, freight) are one row.</summary>
    Item = 1,

    /// <summary>One row for each item category; items with none, and lines with no item, are one row.</summary>
    Category = 2,

    /// <summary>One row for each vendor.</summary>
    Vendor = 3,
}
