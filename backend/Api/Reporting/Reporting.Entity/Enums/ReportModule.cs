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
}
