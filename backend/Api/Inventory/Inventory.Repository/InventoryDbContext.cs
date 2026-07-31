using Inventory.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Inventory.Repository;

/// <summary>
/// The inv schema, in a per-customer database. The base class supplies the
/// OrgId query filter, the insert-time OrgId stamp and xmin concurrency, so
/// nothing here needs to remember them.
/// </summary>
public class InventoryDbContext : TenantDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<UomType> UomTypes => Set<UomType>();

    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();

    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

    public DbSet<MetalPurity> MetalPurities => Set<MetalPurity>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemJewelleryDetails> ItemJewelleryDetails => Set<ItemJewelleryDetails>();

    public DbSet<ItemPharmaDetails> ItemPharmaDetails => Set<ItemPharmaDetails>();

    public DbSet<ItemBarcode> ItemBarcodes => Set<ItemBarcode>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    /// <summary>
    /// Mapped, not migrated. Accounting owns this table; Inventory maps the same
    /// Shared.Kernel entity so an item code is allocated inside the same
    /// transaction as the item insert.
    /// </summary>
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inv");

        modelBuilder.ConfigureNumberingSeries(ownsMigration: false);

        modelBuilder.Entity<UomType>(b =>
        {
            b.HasKey(e => e.UomTypeId);
            b.HasIndex(e => new { e.OrgId, e.UomTypeName }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.UomTypeSystemName })
                .IsUnique()
                .HasFilter("\"UomTypeSystemName\" IS NOT NULL")
                .HasDatabaseName("IX_UomTypes_SystemName");

            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.UomTypeName })
                .HasDatabaseName("IX_UomTypes_Order");
        });

        modelBuilder.Entity<UnitOfMeasure>(b =>
        {
            b.HasKey(e => e.UomId);
            b.HasIndex(e => new { e.OrgId, e.UomCode }).IsUnique();

            // At most one base unit per type. At least one is a C# rule — it
            // spans rows, so no constraint can express it.
            b.HasIndex(e => new { e.OrgId, e.UomTypeId })
                .IsUnique()
                .HasFilter("\"IsBaseUnit\" = true")
                .HasDatabaseName("IX_UnitOfMeasures_BaseUnit");

            b.HasIndex(e => new { e.OrgId, e.UomSystemName })
                .IsUnique()
                .HasFilter("\"UomSystemName\" IS NOT NULL")
                .HasDatabaseName("IX_UnitOfMeasures_SystemName");

            b.HasIndex(e => new { e.OrgId, e.UomTypeId, e.DisplayOrder, e.UomCode })
                .HasDatabaseName("IX_UnitOfMeasures_Order");

            b.Property(e => e.ConversionToBase).HasColumnType("decimal(18,6)");

            b.HasOne<UomType>()
                .WithMany()
                .HasForeignKey(e => e.UomTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // A factor of zero would make every conversion through this unit
                // either zero or a division by zero.
                table.HasCheckConstraint(
                    "chk_uom_conversion_positive",
                    "\"ConversionToBase\" > 0");

                // The base unit defines the scale, so it cannot be on a different one.
                table.HasCheckConstraint(
                    "chk_uom_base_factor",
                    "\"IsBaseUnit\" = false OR \"ConversionToBase\" = 1");

                table.HasCheckConstraint(
                    "chk_uom_decimals",
                    "\"DecimalPlaces\" BETWEEN 0 AND 6");
            });
        });

        modelBuilder.Entity<ItemCategory>(b =>
        {
            b.HasKey(e => e.ItemCategoryId);
            b.HasIndex(e => new { e.OrgId, e.CategoryCode }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ParentCategoryId, e.DisplayOrder, e.CategoryName })
                .HasDatabaseName("IX_ItemCategories_Order");

            b.Property(e => e.DefaultItemProfile).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.DefaultCostingType).HasConversion<string>().HasMaxLength(20);

            b.HasOne<ItemCategory>()
                .WithMany()
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MetalPurity>(b =>
        {
            b.HasKey(e => e.MetalPurityId);
            b.HasIndex(e => new { e.OrgId, e.MetalType, e.PurityName }).IsUnique();

            b.HasIndex(e => new { e.OrgId, e.MetalType, e.DisplayOrder })
                .HasDatabaseName("IX_MetalPurities_Order");

            b.Property(e => e.MetalType).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.PurityFactor).HasColumnType("decimal(6,4)");

            b.ToTable(table => table.HasCheckConstraint(
                "chk_purity_factor",
                "\"PurityFactor\" > 0 AND \"PurityFactor\" <= 1"));
        });

        modelBuilder.Entity<Item>(b =>
        {
            b.HasKey(e => e.ItemId);
            b.HasIndex(e => new { e.OrgId, e.ItemCode }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ItemName });
            b.HasIndex(e => new { e.OrgId, e.ItemCategoryId });
            b.HasIndex(e => new { e.OrgId, e.HsnSacCodeId });
            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.ItemName })
                .HasDatabaseName("IX_Items_Order");

            b.HasIndex(e => e.OrgId).HasFilter("\"IsSales\" = true").HasDatabaseName("IX_Items_Sales");
            b.HasIndex(e => e.OrgId).HasFilter("\"IsPurchase\" = true").HasDatabaseName("IX_Items_Purchase");

            b.Property(e => e.ItemProfile).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.ItemType).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.TaxPreference).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.CostingType).HasConversion<string>().HasMaxLength(25);

            foreach (string money in new[]
                { "SalesPrice", "PurchasePrice", "MinSalePrice", "StandardCost" })
            {
                b.Property(money).HasColumnType("decimal(18,4)");
            }

            b.Property(e => e.Mrp).HasColumnType("decimal(18,2)");

            foreach (string qty in new[]
                { "ReorderLevel", "ReorderQuantity", "MinStockLevel", "MaxStockLevel" })
            {
                b.Property(qty).HasColumnType("decimal(18,3)");
            }

            b.HasOne<UomType>().WithMany().HasForeignKey(e => e.UomTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(e => e.InventoryUomId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ItemCategory>().WithMany().HasForeignKey(e => e.ItemCategoryId).OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // A service with a costing method, or a stocked item without
                // one, are both nonsense.
                table.HasCheckConstraint(
                    "chk_item_costing_tracking",
                    "(\"TrackInventory\" = false AND \"CostingType\" = 'None') "
                        + "OR (\"TrackInventory\" = true AND \"CostingType\" <> 'None')");

                // An expiry date with no batch to attach it to means nothing.
                table.HasCheckConstraint(
                    "chk_item_expiry_needs_batch",
                    "\"IsExpiryTracked\" = false OR \"IsBatchTracked\" = true");

                // Layered costing with nothing to layer on has no way to pick a cost.
                table.HasCheckConstraint(
                    "chk_item_fefo_tracking",
                    "\"CostingType\" <> 'Fefo' "
                        + "OR (\"IsBatchTracked\" = true AND \"IsExpiryTracked\" = true)");

                table.HasCheckConstraint(
                    "chk_item_specific_tracking",
                    "\"CostingType\" <> 'SpecificIdentification' OR \"IsSerialTracked\" = true");

                table.HasCheckConstraint(
                    "chk_item_min_sale_price",
                    "\"MinSalePrice\" IS NULL OR \"SalesPrice\" IS NULL "
                        + "OR \"MinSalePrice\" <= \"SalesPrice\"");
            });
        });

        modelBuilder.Entity<ItemJewelleryDetails>(b =>
        {
            // ItemId is key and foreign key both, so a second row for one item
            // is structurally impossible.
            b.HasKey(e => e.ItemId);
            b.Property(e => e.ItemId).ValueGeneratedNever();

            b.Property(e => e.MetalType).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.MakingChargeType).HasConversion<string>().HasMaxLength(15);

            foreach (string weight in new[] { "GrossWeight", "NetWeight", "StoneWeight" })
            {
                b.Property(weight).HasColumnType("decimal(12,3)");
            }

            b.Property(e => e.StoneCharge).HasColumnType("decimal(18,2)");
            b.Property(e => e.WastagePercent).HasColumnType("decimal(5,2)");
            b.Property(e => e.MakingChargeValue).HasColumnType("decimal(18,4)");

            b.HasOne<Item>()
                .WithOne()
                .HasForeignKey<ItemJewelleryDetails>(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<MetalPurity>()
                .WithMany()
                .HasForeignKey(e => e.MetalPurityId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "chk_jewellery_weights",
                    "\"NetWeight\" <= \"GrossWeight\" AND \"StoneWeight\" <= \"GrossWeight\"");

                table.HasCheckConstraint(
                    "chk_jewellery_making_percent",
                    "\"MakingChargeType\" <> 'Percentage' OR \"MakingChargeValue\" <= 100");
            });
        });

        modelBuilder.Entity<ItemPharmaDetails>(b =>
        {
            b.HasKey(e => e.ItemId);
            b.Property(e => e.ItemId).ValueGeneratedNever();

            b.Property(e => e.DrugSchedule).HasConversion<string>().HasMaxLength(10);
            b.Property(e => e.DosageForm).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.StorageCondition).HasConversion<string>().HasMaxLength(15);

            b.HasIndex(e => new { e.OrgId, e.GenericName })
                .HasDatabaseName("IX_ItemPharmaDetails_Generic");

            b.HasOne<Item>()
                .WithOne()
                .HasForeignKey<ItemPharmaDetails>(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_pharma_prescription",
                "\"DrugSchedule\" NOT IN ('H', 'H1', 'X') OR \"IsPrescriptionRequired\" = true"));
        });

        modelBuilder.Entity<ItemBarcode>(b =>
        {
            b.HasKey(e => e.ItemBarcodeId);

            // One scan, one item. A shared barcode makes point of sale ambiguous.
            b.HasIndex(e => new { e.OrgId, e.Barcode }).IsUnique();
            b.HasIndex(e => new { e.OrgId, e.ItemId });

            b.HasIndex(e => new { e.OrgId, e.ItemId })
                .IsUnique()
                .HasFilter("\"IsPrimary\" = true")
                .HasDatabaseName("IX_ItemBarcodes_Primary");

            b.Property(e => e.BarcodeType).HasConversion<string>().HasMaxLength(20);

            b.HasOne<Item>()
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Warehouse>(b =>
        {
            b.HasKey(e => e.WarehouseId);
            b.HasIndex(e => new { e.OrgId, e.WarehouseCode }).IsUnique();

            b.HasIndex(e => e.OrgId)
                .IsUnique()
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_Warehouses_Default");

            b.HasIndex(e => new { e.OrgId, e.DisplayOrder, e.WarehouseName })
                .HasDatabaseName("IX_Warehouses_Order");

            b.Property(e => e.WarehouseType).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.StorageType).HasConversion<string>().HasMaxLength(15);
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
