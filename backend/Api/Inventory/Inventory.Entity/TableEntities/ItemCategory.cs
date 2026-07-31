using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// Groups items for reporting, and defaults the three fields nobody wants to set
/// item by item. The defaults are copied onto an item at creation and then
/// independent — changing a category's default never rewrites existing items,
/// because that would restate costing on live stock.
/// </summary>
public class ItemCategory : OrgScopedEntity
{
    public long ItemCategoryId { get; set; }

    [Required(ErrorMessage = "Category code is required.")]
    [MaxLength(20, ErrorMessage = "Category code cannot exceed 20 characters.")]
    public string CategoryCode { get; set; } = null!;

    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    public string CategoryName { get; set; } = null!;

    /// <summary>Null is a top-level category. Maximum depth is three.</summary>
    public long? ParentCategoryId { get; set; }

    public ItemProfile? DefaultItemProfile { get; set; }

    public CostingType? DefaultCostingType { get; set; }

    public long? DefaultUomTypeId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
