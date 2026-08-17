using Inventory.Entity.TableEntities;

namespace Inventory.Repository.SeedData;

/// <summary>
/// Unit types and their units, written at organization creation. Two passes,
/// because a unit needs its type's generated id: insert the types, then feed
/// their ids back in for the units.
///
/// Every UqcCode here is the notified GST unit the row reports as. Carat and
/// tola have no notified equivalent and report as OTH — which is exactly why
/// UomCode and UqcCode are separate columns.
/// </summary>
public static class UomSeed
{
    public static IReadOnlyList<UomType> BuildTypes(Guid orgId) =>
    [
        Type(orgId, 10, "QUANTITY", "Quantity"),
        Type(orgId, 20, "WEIGHT", "Weight"),
        Type(orgId, 30, "VOLUME", "Volume"),
        Type(orgId, 40, "LENGTH", "Length"),
        Type(orgId, 50, "AREA", "Area"),
        Type(orgId, 60, "TIME", "Time"),
    ];

    /// <param name="typeIds">
    /// Type id by system name, as the organization actually has them — the rows
    /// just inserted plus any that were already there.
    ///
    /// A name absent from this map means the organization has no such type, and
    /// its units are skipped rather than looked up and thrown on. That happens
    /// when a type could not be seeded because a customer-created type already
    /// held its name; the alternative is one name clash taking down the whole
    /// seeding call, which during provisioning fails the branch.
    /// </param>
    public static IReadOnlyList<UnitOfMeasure> BuildUnits(
        Guid orgId, IReadOnlyDictionary<string, long> typeIds)
    {
        var units = new List<UnitOfMeasure>();

        // Quantity. The container units carry a factor of 1 deliberately: a box
        // has no universal size, so a pack is its own unit with its own factor
        // (a 50 kg bag is a Weight unit of 50), not a fudge on this one.
        Add(units, orgId, typeIds, "QUANTITY",
        [
            ("PCS", "PCS", "Pieces", 1m, 0, true),
            ("NOS", "NOS", "Numbers", 1m, 0, false),
            ("UNT", "UNT", "Units", 1m, 0, false),
            ("SET", "SET", "Sets", 1m, 0, false),
            ("PRS", "PRS", "Pairs", 2m, 0, false),
            ("DOZ", "DOZ", "Dozens", 12m, 0, false),
            ("GRS", "GRS", "Gross", 144m, 0, false),
            ("THD", "THD", "Thousands", 1000m, 0, false),
            ("BOX", "BOX", "Box", 1m, 0, false),
            ("PAC", "PAC", "Packs", 1m, 0, false),
            ("CTN", "CTN", "Cartons", 1m, 0, false),
            ("BAG", "BAG", "Bags", 1m, 0, false),
            ("BDL", "BDL", "Bundles", 1m, 0, false),
            ("ROL", "ROL", "Rolls", 1m, 0, false),
            ("BTL", "BTL", "Bottles", 1m, 0, false),
            ("CAN", "CAN", "Cans", 1m, 0, false),
        ]);

        // Weight. Carat and tola are the jewellery additions.
        Add(units, orgId, typeIds, "WEIGHT",
        [
            ("KGS", "KGS", "Kilograms", 1m, 3, true),
            ("GMS", "GMS", "Grams", 0.001m, 3, false),
            ("QTL", "QTL", "Quintal", 100m, 3, false),
            ("TON", "TON", "Tonnes", 1000m, 3, false),
            ("MTS", "MTS", "Metric ton", 1000m, 3, false),
            ("CRT", "OTH", "Carat", 0.0002m, 3, false),
            ("TOL", "OTH", "Tola", 0.0116638m, 3, false),
        ]);

        Add(units, orgId, typeIds, "VOLUME",
        [
            ("LTR", "LTR", "Litres", 1m, 3, true),
            ("MLT", "MLT", "Millilitres", 0.001m, 3, false),
            ("KLR", "KLR", "Kilolitres", 1000m, 3, false),
            ("CBM", "CBM", "Cubic metres", 1000m, 3, false),
            ("CCM", "CCM", "Cubic centimetres", 0.000001m, 3, false),
            ("UGS", "UGS", "US gallons", 3.785412m, 3, false),
        ]);

        Add(units, orgId, typeIds, "LENGTH",
        [
            ("MTR", "MTR", "Metres", 1m, 3, true),
            ("CMS", "CMS", "Centimetres", 0.01m, 3, false),
            ("KME", "KME", "Kilometres", 1000m, 3, false),
            ("YDS", "YDS", "Yards", 0.9144m, 3, false),
        ]);

        Add(units, orgId, typeIds, "AREA",
        [
            ("SQM", "SQM", "Square metres", 1m, 3, true),
            ("SQF", "SQF", "Square feet", 0.092903m, 3, false),
            ("SQY", "SQY", "Square yards", 0.836127m, 3, false),
        ]);

        // Service billing. The month factor is an average, not a fact — nothing
        // should convert a price through it.
        Add(units, orgId, typeIds, "TIME",
        [
            ("HRS", "OTH", "Hours", 1m, 2, true),
            ("DAY", "OTH", "Days", 24m, 2, false),
            ("MON", "OTH", "Months", 730m, 2, false),
        ]);

        return units;
    }

    private static void Add(
        List<UnitOfMeasure> units,
        Guid orgId,
        IReadOnlyDictionary<string, long> typeIds,
        string typeSystemName,
        IReadOnlyList<(string Code, string Uqc, string Name, decimal Factor, int Decimals, bool IsBase)> rows)
    {
        if (!typeIds.TryGetValue(typeSystemName, out long uomTypeId))
        {
            return;
        }

        int order = 10;
        foreach (var row in rows)
        {
            units.Add(new UnitOfMeasure
            {
                OrgId = orgId,
                UomTypeId = uomTypeId,
                UomSystemName = row.Code,
                UomCode = row.Code,
                UqcCode = row.Uqc,
                UomName = row.Name,
                IsBaseUnit = row.IsBase,
                ConversionToBase = row.Factor,
                DecimalPlaces = row.Decimals,
                DisplayOrder = order,
                IsSystem = true,
                IsActive = true,
            });

            order += 10;
        }
    }

    private static UomType Type(Guid orgId, int displayOrder, string systemName, string name) =>
        new()
        {
            OrgId = orgId,
            UomTypeSystemName = systemName,
            UomTypeName = name,
            DisplayOrder = displayOrder,
            IsSystem = true,
            IsActive = true,
        };
}
