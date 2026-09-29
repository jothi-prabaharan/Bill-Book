using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;

namespace Accounting.Repository.SeedData;

/// <summary>
/// The TDS sections written when a branch is created (TK-117, D-32): the
/// familiar codes with the rates and thresholds of the 2025-26 Finance Act,
/// effective 1 April 2025. Every row is editable — the Income-tax Act 2025
/// renumbers these provisions from 1 April 2026, and the owner re-labels or
/// re-dates them on the TDS sections screen rather than the code guessing.
///
/// Section 194J is two codes because its two limbs carry different rates, and
/// 194I two for the same reason. Rent's threshold is per month, so it is a
/// single-document threshold here and has no aggregate.
/// </summary>
public static class TdsSectionSeed
{
    public static readonly DateOnly EffectiveFrom = new(2025, 4, 1);

    public static IReadOnlyList<TdsSection> Build(Guid orgId) =>
    [
        Section(orgId, "194C", "Payment to contractors and sub-contractors", 1m, 2m, 20m, 30_000m, 100_000m),
        Section(orgId, "194JA", "Fees for technical services, call centre, royalty for films", 2m, 2m, 20m, null, 50_000m),
        Section(orgId, "194JB", "Fees for professional services", 10m, 10m, 20m, null, 50_000m),
        Section(orgId, "194H", "Commission or brokerage", 2m, 2m, 20m, null, 20_000m),
        Section(orgId, "194I(a)", "Rent of plant, machinery or equipment", 2m, 2m, 20m, 50_000m, null),
        Section(orgId, "194I(b)", "Rent of land, building or furniture", 10m, 10m, 20m, 50_000m, null),
        Section(orgId, "194A", "Interest other than interest on securities", 10m, 10m, 20m, null, 10_000m),
        Section(orgId, "194Q", "Purchase of goods", 0.1m, 0.1m, 5m, null, 5_000_000m, TdsThresholdBasis.ExcessOnly),
    ];

    private static TdsSection Section(
        Guid orgId,
        string code,
        string nature,
        decimal individual,
        decimal other,
        decimal noPan,
        decimal? single,
        decimal? aggregate,
        TdsThresholdBasis basis = TdsThresholdBasis.WholeAmount) =>
        new()
        {
            OrgId = orgId,
            SectionCode = code,
            NatureOfPayment = nature,
            EffectiveFrom = EffectiveFrom,
            RateIndividual = individual,
            RateOther = other,
            RateNoPan = noPan,
            SingleThreshold = single,
            AggregateThreshold = aggregate,
            ThresholdBasis = basis,
            IsActive = true,
        };
}
