using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.DepreciationSchedules</c>, read-only. At most one <c>Books</c> and one
/// <c>Tax</c> schedule per asset, which a unique index in Accounting enforces.
///
/// <b>Only the <c>Books</c> schedule is ever charged</b> —
/// <c>DepreciationService</c> reads nothing else — so it is the one the
/// fixed-asset reports describe.
/// </summary>
public class DepreciationScheduleRead : OrgScopedEntity
{
    public long DepreciationScheduleId { get; set; }

    public long FixedAssetId { get; set; }

    public DepreciationScheduleType ScheduleType { get; set; }

    public DepreciationMethod DepreciationMethod { get; set; }

    /// <summary>A percentage — <c>15.00</c> is fifteen percent.</summary>
    public decimal Rate { get; set; }

    public int UsefulLifeYears { get; set; }

    public DateOnly DepreciationStartDate { get; set; }

    public decimal SalvageValue { get; set; }
}
