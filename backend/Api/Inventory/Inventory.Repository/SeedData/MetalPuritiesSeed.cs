using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;

namespace Inventory.Repository.SeedData;

/// <summary>
/// The standard Indian purities, ordered finest first because that is how a
/// jeweller reads a list. Only seeded for an organization whose vertical is
/// jewellery — a chemist has no use for them.
/// </summary>
public static class MetalPuritiesSeed
{
    public static IReadOnlyList<MetalPurity> Build(Guid orgId) =>
    [
        Purity(orgId, MetalType.Gold, 10, "GOLD_999", "999 (24K)", 0.9990m),
        Purity(orgId, MetalType.Gold, 20, "GOLD_958", "958 (23K)", 0.9580m),
        Purity(orgId, MetalType.Gold, 30, "GOLD_916", "916 (22K)", 0.9160m),
        Purity(orgId, MetalType.Gold, 40, "GOLD_750", "750 (18K)", 0.7500m),
        Purity(orgId, MetalType.Gold, 50, "GOLD_585", "585 (14K)", 0.5850m),
        Purity(orgId, MetalType.Gold, 60, "GOLD_375", "375 (9K)", 0.3750m),

        Purity(orgId, MetalType.Silver, 10, "SILVER_999", "999 Fine", 0.9990m),
        Purity(orgId, MetalType.Silver, 20, "SILVER_925", "925 Sterling", 0.9250m),
        Purity(orgId, MetalType.Silver, 30, "SILVER_900", "900 Coin", 0.9000m),

        Purity(orgId, MetalType.Platinum, 10, "PLATINUM_950", "950 Platinum", 0.9500m),
        Purity(orgId, MetalType.Platinum, 20, "PLATINUM_900", "900 Platinum", 0.9000m),
    ];

    private static MetalPurity Purity(
        Guid orgId,
        MetalType metal,
        int displayOrder,
        string systemName,
        string name,
        decimal factor) =>
        new()
        {
            OrgId = orgId,
            MetalType = metal,
            PuritySystemName = systemName,
            PurityName = name,
            PurityFactor = factor,
            DisplayOrder = displayOrder,
            IsSystem = true,
            IsActive = true,
        };
}
