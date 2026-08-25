using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

public class FixedAssetCategory : OrgScopedEntity
{
    public long FixedAssetCategoryId { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(200, ErrorMessage = "Category name cannot exceed 200 characters.")]
    public string CategoryName { get; set; } = null!;

    public long AssetAccountId { get; set; }
    public long AccumulatedDepreciationAccountId { get; set; }
    public long DepreciationExpenseAccountId { get; set; }
}
