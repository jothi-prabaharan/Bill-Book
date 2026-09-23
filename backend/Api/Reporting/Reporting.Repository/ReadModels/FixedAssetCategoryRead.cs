#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.FixedAssetCategories</c>, read-only. What the reports call the
/// <i>asset type</i>.
///
/// <b>The category owns the ledger mapping, not the asset</b> — its three
/// accounts are where an asset in it is carried, where its depreciation
/// accumulates and where the charge is expensed. The reconciliation reads the
/// register against the ledger through exactly these three columns.
/// </summary>
public class FixedAssetCategoryRead : OrgScopedEntity
{
    public long FixedAssetCategoryId { get; set; }

    public string CategoryName { get; set; }

    public long AssetAccountId { get; set; }

    public long AccumulatedDepreciationAccountId { get; set; }

    public long DepreciationExpenseAccountId { get; set; }
}
