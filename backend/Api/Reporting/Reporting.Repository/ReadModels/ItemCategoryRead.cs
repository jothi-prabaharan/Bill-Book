#pragma warning disable CS8618

using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// Groups items for reporting, and defaults the three fields nobody wants to set
/// item by item. The defaults are copied onto an item at creation and then
/// independent — changing a category's default never rewrites existing items,
/// because that would restate costing on live stock.
/// </summary>
public class ItemCategoryRead : OrgScopedEntity
{
    public long ItemCategoryId { get; set; }


    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    /// <summary>Null is a top-level category. Maximum depth is three.</summary>
    public long? ParentCategoryId { get; set; }

    public int? DefaultItemProfile { get; set; }

    public int? DefaultCostingType { get; set; }

    public long? DefaultUomTypeId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}




