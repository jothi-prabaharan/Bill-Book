namespace Reporting.Entity.Enums;

/// <summary>
/// Which part of the product a report belongs to. Groups the catalog screen,
/// and is not the same thing as the permission a report requires — Bank Summary
/// is a <see cref="Banking"/> report that needs <c>accounting.view</c>, because
/// banking lives in the acc schema.
/// </summary>
public enum ReportModule
{
    Accounting = 1,
    Banking = 2,
    Inventory = 3,
    Sales = 4,
    Purchase = 5,
    FixedAssets = 6,
    People = 7,
    Time = 8,
    Leave = 9,
    Pay = 10,
    Statutory = 11,
    Recruitment = 12,
    Claims = 13,

    /// <summary>Project accounting (TK-107): profitability, budgets, unbilled work and time.</summary>
    Projects = 14,

    /// <summary>The report builder's datasets and the custom reports saved from them (TK-113).</summary>
    Custom = 15,

    /// <summary>A Restaurant branch's day-end, sales, kitchen and food cost reports (TK-150).</summary>
    Restaurant = 16,

    /// <summary>A Service branch's site reports (TK-176).</summary>
    Site = 17,
}
