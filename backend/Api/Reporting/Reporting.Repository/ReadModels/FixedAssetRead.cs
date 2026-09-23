#pragma warning disable CS8618
using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.FixedAssets</c>, read-only. One asset on the register.
///
/// <b><c>PurchasePrice</c> is the asset's cost</b>, and the only cost there is:
/// the register holds no revaluation or cost adjustment that would make the
/// two differ. <c>Status</c> is stored by name, so it maps through the mirror
/// enum rather than an int.
/// </summary>
public class FixedAssetRead : OrgScopedEntity
{
    public long FixedAssetId { get; set; }

    public long FixedAssetCategoryId { get; set; }

    public string AssetCode { get; set; }

    public string AssetName { get; set; }

    public string? Description { get; set; }

    public string? SerialNumber { get; set; }

    public DateOnly PurchaseDate { get; set; }

    public decimal PurchasePrice { get; set; }

    public long? PurchaseBillId { get; set; }

    public FixedAssetStatus Status { get; set; }
}
