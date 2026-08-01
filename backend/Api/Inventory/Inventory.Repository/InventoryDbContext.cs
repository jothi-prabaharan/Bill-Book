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

    public DbSet<ItemStock> ItemStock => Set<ItemStock>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<ItemBatch> ItemBatches => Set<ItemBatch>();

    public DbSet<ItemSerial> ItemSerials => Set<ItemSerial>();

    public DbSet<CostLayer> CostLayers => Set<CostLayer>();

    public DbSet<CostLayerConsumption> CostLayerConsumptions => Set<CostLayerConsumption>();

    public DbSet<RecostingAdjustment> RecostingAdjustments => Set<RecostingAdjustment>();

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

        modelBuilder.Entity<ItemStock>(b =>
        {
            // ItemId is key and foreign key both: one item, one stock row, with
            // no way to write a second.
            b.HasKey(e => e.ItemId);
            b.Property(e => e.ItemId).ValueGeneratedNever();

            b.Property(e => e.QuantityOnHand).HasColumnType("decimal(18,3)");
            b.Property(e => e.WeightedAverageCost).HasColumnType("decimal(18,6)");

            b.HasOne<Item>()
                .WithOne()
                .HasForeignKey<ItemStock>(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            b.ToTable(table =>
            {
                // The conditional decrement already guarantees this; the
                // constraint is what turns a costing bug into a loud failure
                // rather than negative stock nobody notices.
                table.HasCheckConstraint(
                    "chk_item_stock_non_negative",
                    "\"QuantityOnHand\" >= 0 AND \"WeightedAverageCost\" >= 0");
            });
        });

        modelBuilder.Entity<StockMovement>(b =>
        {
            b.HasKey(e => e.StockMovementId);

            // The item's ledger, and the query behind the config lock.
            b.HasIndex(e => new { e.OrgId, e.ItemId, e.MovementDate })
                .HasDatabaseName("IX_StockMovements_Item");

            b.HasIndex(e => new { e.OrgId, e.WarehouseId, e.ItemId })
                .HasDatabaseName("IX_StockMovements_Warehouse");

            b.HasIndex(e => new { e.OrgId, e.MovementDate })
                .HasDatabaseName("IX_StockMovements_Date");

            // Idempotency. Service Bus is at-least-once, so a redelivered event
            // must not move stock twice — the second insert hits this index and
            // fails rather than doubling the quantity. Filtered, because a
            // manual adjustment has no source document.
            b.HasIndex(e => new { e.OrgId, e.SourceType, e.SourceId, e.SourceLineId })
                .IsUnique()
                .HasFilter("\"SourceType\" IS NOT NULL")
                .HasDatabaseName("IX_StockMovements_Source");

            b.Property(e => e.MovementType).HasConversion<string>().HasMaxLength(15);
            b.Property(e => e.Direction).HasConversion<string>().HasMaxLength(3);

            foreach (string qty in new[] { "EnteredQuantity", "Quantity" })
            {
                b.Property(qty).HasColumnType("decimal(18,3)");
            }

            b.Property(e => e.ConversionFactor).HasColumnType("decimal(18,6)");
            b.Property(e => e.UnitCost).HasColumnType("decimal(18,6)");
            b.Property(e => e.ResultingWeightedAverageCost).HasColumnType("decimal(18,6)");
            b.Property(e => e.TotalCost).HasColumnType("decimal(18,2)");

            b.HasOne<Item>()
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(e => e.EnteredUomId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table =>
            {
                // Positive quantities only; Direction carries the sign. A
                // negative here would reverse a movement with nothing on screen
                // saying so.
                table.HasCheckConstraint(
                    "chk_movement_quantity_positive",
                    "\"Quantity\" > 0 AND \"EnteredQuantity\" > 0 AND \"ConversionFactor\" > 0");

                // The two quantities have to agree, or the entered figure is
                // decoration and the stock figure is unexplainable. Rounded to
                // the stored scale before comparing.
                table.HasCheckConstraint(
                    "chk_movement_conversion",
                    "\"Quantity\" = ROUND(\"EnteredQuantity\" * \"ConversionFactor\", 3)");

                table.HasCheckConstraint(
                    "chk_movement_cost_non_negative",
                    "(\"UnitCost\" IS NULL OR \"UnitCost\" >= 0) "
                        + "AND (\"TotalCost\" IS NULL OR \"TotalCost\" >= 0)");

                // A source id without a type, or the reverse, is half a
                // reference — and the idempotency index would not catch it.
                table.HasCheckConstraint(
                    "chk_movement_source",
                    "(\"SourceType\" IS NULL AND \"SourceId\" IS NULL) "
                        + "OR (\"SourceType\" IS NOT NULL AND \"SourceId\" IS NOT NULL)");
            });
        });

        modelBuilder.Entity<ItemBatch>(b =>
        {
            b.HasKey(e => e.ItemBatchId);

            // One batch number per item. The same number on two lots makes
            // "which one expires first" unanswerable.
            b.HasIndex(e => new { e.OrgId, e.ItemId, e.BatchNumber }).IsUnique();

            // What FEFO orders by. Filtered, because a lot with no expiry never
            // competes to go out first.
            b.HasIndex(e => new { e.OrgId, e.ItemId, e.ExpiryDate })
                .HasFilter("\"ExpiryDate\" IS NOT NULL")
                .HasDatabaseName("IX_ItemBatches_Expiry");

            b.Property(e => e.Mrp).HasColumnType("decimal(18,2)");

            b.HasOne<Item>()
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_batch_dates",
                "\"ManufactureDate\" IS NULL OR \"ExpiryDate\" IS NULL "
                    + "OR \"ExpiryDate\" >= \"ManufactureDate\""));
        });

        modelBuilder.Entity<ItemSerial>(b =>
        {
            b.HasKey(e => e.ItemSerialId);
            b.HasIndex(e => new { e.OrgId, e.ItemId, e.SerialNumber }).IsUnique();

            // A HUID identifies exactly one piece in the country, so two rows
            // carrying the same one is a data-entry error worth refusing.
            b.HasIndex(e => new { e.OrgId, e.HallmarkNumber })
                .IsUnique()
                .HasFilter("\"HallmarkNumber\" IS NOT NULL")
                .HasDatabaseName("IX_ItemSerials_Huid");

            // The picker on a sale: what is still on the shelf.
            b.HasIndex(e => new { e.OrgId, e.ItemId, e.Status })
                .HasDatabaseName("IX_ItemSerials_Status");

            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);

            b.HasOne<Item>()
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<ItemBatch>()
                .WithMany()
                .HasForeignKey(e => e.ItemBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CostLayer>(b =>
        {
            b.HasKey(e => e.CostLayerId);

            // One layer per inbound movement. A second would double the stock
            // available to allocate against without changing the quantity.
            b.HasIndex(e => new { e.OrgId, e.StockMovementId }).IsUnique();

            // FIFO ascending, LIFO descending — one index serves both. Filtered
            // to layers with something left, which is all a selection ever scans.
            b.HasIndex(e => new { e.OrgId, e.ItemId, e.ReceivedOn })
                .HasFilter("\"RemainingQuantity\" > 0")
                .HasDatabaseName("IX_CostLayers_Fifo");

            b.HasIndex(e => new { e.OrgId, e.ItemId, e.ExpiresOn })
                .HasFilter("\"RemainingQuantity\" > 0 AND \"ExpiresOn\" IS NOT NULL")
                .HasDatabaseName("IX_CostLayers_Fefo");

            foreach (string qty in new[] { "OriginalQuantity", "RemainingQuantity" })
            {
                b.Property(qty).HasColumnType("decimal(18,3)");
            }

            b.Property(e => e.UnitCost).HasColumnType("decimal(18,6)");

            b.HasOne<Item>()
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(e => e.StockMovementId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<ItemBatch>()
                .WithMany()
                .HasForeignKey(e => e.ItemBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_layer_remaining",
                "\"RemainingQuantity\" >= 0 AND \"RemainingQuantity\" <= \"OriginalQuantity\" "
                    + "AND \"OriginalQuantity\" > 0 AND \"UnitCost\" >= 0"));
        });

        modelBuilder.Entity<CostLayerConsumption>(b =>
        {
            b.HasKey(e => e.CostLayerConsumptionId);

            // One *current* allocation per layer per issue. Filtered on
            // SupersededAt, because a recosting keeps the row it replaced —
            // without the filter the replay could never write its own.
            b.HasIndex(e => new { e.OrgId, e.StockMovementId, e.CostLayerId })
                .IsUnique()
                .HasFilter("\"SupersededAt\" IS NULL")
                .HasDatabaseName("IX_CostLayerConsumptions_Allocation");

            // Summing an issue's COGS, and finding what to unwind when a
            // backdated receipt invalidates the allocations after it.
            b.HasIndex(e => new { e.OrgId, e.StockMovementId })
                .HasDatabaseName("IX_CostLayerConsumptions_Movement");

            b.HasIndex(e => new { e.OrgId, e.CostLayerId })
                .HasDatabaseName("IX_CostLayerConsumptions_Layer");

            b.Property(e => e.Quantity).HasColumnType("decimal(18,3)");
            b.Property(e => e.UnitCost).HasColumnType("decimal(18,6)");
            b.Property(e => e.TotalCost).HasColumnType("decimal(18,2)");

            b.HasOne<CostLayer>()
                .WithMany()
                .HasForeignKey(e => e.CostLayerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(e => e.StockMovementId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_consumption_amounts",
                "\"Quantity\" > 0 AND \"UnitCost\" >= 0 AND \"TotalCost\" >= 0"));
        });

        modelBuilder.Entity<RecostingAdjustment>(b =>
        {
            b.HasKey(e => e.RecostingAdjustmentId);

            // Reading one run as a whole, which is how it is reviewed.
            b.HasIndex(e => new { e.OrgId, e.RecostingBatchId })
                .HasDatabaseName("IX_RecostingAdjustments_Batch");

            b.HasIndex(e => new { e.OrgId, e.ItemId, e.RunAt })
                .HasDatabaseName("IX_RecostingAdjustments_Item");

            b.HasIndex(e => new { e.OrgId, e.StockMovementId })
                .HasDatabaseName("IX_RecostingAdjustments_Movement");

            foreach (string money in new[] { "PreviousCost", "NewCost", "Delta" })
            {
                b.Property(money).HasColumnType("decimal(18,2)");
            }

            b.HasOne<Item>()
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(e => e.StockMovementId)
                .OnDelete(DeleteBehavior.Restrict);

            b.ToTable(table => table.HasCheckConstraint(
                "chk_recosting_delta",
                "\"Delta\" = \"NewCost\" - \"PreviousCost\""));
        });

        // Base class applies query filters, OrgId indexes and xmin last so it
        // sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
