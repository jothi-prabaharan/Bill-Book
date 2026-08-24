#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.TaxMaster</c>, read-only. The effective-dated GST rates.
///
/// <b>Reached through a sub-account, not through the ledger leg.</b> A GST leg
/// carries a <c>SubAccountId</c> whose <c>ReferenceType</c> is Tax and whose
/// <c>ReferenceId</c> is the row below — that indirection is the only link a
/// posting has to the rate it was taxed at, because the leg stores the amount
/// rather than the rate.
///
/// <b>The table is effective-dated, so a report joining to it must not assume one
/// row per tax.</b> A rate revision inserts a new row with a later
/// <c>EffectiveFrom</c>; joining on the id alone is right, because the posting's
/// sub-account already names the row that was in force on the day.
/// </summary>
public class TaxMasterRead : OrgScopedEntity
{
    public long TaxMasterId { get; set; }

    public long TaxGroupId { get; set; }

    public string TaxName { get; set; }

    /// <summary>CGST + SGST, or IGST — the figure a report shows as the rate.</summary>
    public decimal TotalRate { get; set; }

    public decimal CgstRate { get; set; }

    public decimal SgstRate { get; set; }

    public decimal IgstRate { get; set; }

    public decimal CessRate { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    /// <summary>A rate can apply to one direction, both, or — rarely — neither yet.</summary>
    public bool IsSales { get; set; }

    public bool IsPurchase { get; set; }
}
