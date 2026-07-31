using Accounting.Entity.TableEntities;

namespace Accounting.Repository.SeedData;

/// <summary>
/// The GST rates written when an organization is created. Per-organization, so
/// inserted at org creation rather than by migration.
///
/// The 3% bullion rate is an ordinary row — nothing in the schema privileges the
/// standard 0/5/12/18/28 slabs.
/// </summary>
public static class TaxMasterSeed
{
    public static IReadOnlyList<TaxMaster> Build(Guid orgId, DateOnly effectiveFrom) =>
    [
        Rate(orgId, "GST0", "GST 0%", 0m, effectiveFrom),
        Rate(orgId, "GST5", "GST 5%", 5m, effectiveFrom),
        Rate(orgId, "GST12", "GST 12%", 12m, effectiveFrom),
        Rate(orgId, "GST18", "GST 18%", 18m, effectiveFrom),
        Rate(orgId, "GST28", "GST 28%", 28m, effectiveFrom),
        Rate(orgId, "BULLION3", "Bullion 3%", 3m, effectiveFrom),
    ];

    private static TaxMaster Rate(
        Guid orgId, string systemName, string displayName, decimal total, DateOnly from) =>
        new()
        {
            OrgId = orgId,
            TaxSystemName = systemName,
            TaxName = displayName,
            TotalRate = total,
            // Intra-state splits in half; inter-state carries the whole rate.
            CgstRate = total / 2m,
            SgstRate = total / 2m,
            IgstRate = total,
            CessRate = 0m,
            EffectiveFrom = from,
            EffectiveTo = null,
            IsSales = true,
            IsPurchase = true,
            IsActive = true,
        };
}
