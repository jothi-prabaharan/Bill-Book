using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inv");
            migrationBuilder.CreateTable(
                name: "UomTypes",
                schema: "inv",
                columns: table => new
                {
                    UomTypeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UomTypeSystemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    UomTypeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UomTypes", x => x.UomTypeId);
                });
            migrationBuilder.CreateTable(
                name: "ItemCategories",
                schema: "inv",
                columns: table => new
                {
                    ItemCategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParentCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    DefaultItemProfile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    DefaultCostingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultUomTypeId = table.Column<long>(type: "bigint", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCategories", x => x.ItemCategoryId);
                    table.ForeignKey(
                        name: "FK_ItemCategories_ItemCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalSchema: "inv",
                        principalTable: "ItemCategories",
                        principalColumn: "ItemCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "MetalPurities",
                schema: "inv",
                columns: table => new
                {
                    MetalPurityId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetalType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PuritySystemName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PurityName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurityFactor = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalPurities", x => x.MetalPurityId);
                    table.CheckConstraint("chk_purity_factor", "\"PurityFactor\" > 0 AND \"PurityFactor\" <= 1");
                });
            migrationBuilder.CreateTable(
                name: "Warehouses",
                schema: "inv",
                columns: table => new
                {
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarehouseCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WarehouseName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WarehouseType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    StorageType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateId = table.Column<int>(type: "integer", nullable: true),
                    CountryId = table.Column<int>(type: "integer", nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ContactPersonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.WarehouseId);
                });
            migrationBuilder.CreateTable(
                name: "UnitOfMeasures",
                schema: "inv",
                columns: table => new
                {
                    UomId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UomTypeId = table.Column<long>(type: "bigint", nullable: false),
                    UomSystemName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UomCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UqcCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UomName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsBaseUnit = table.Column<bool>(type: "boolean", nullable: false),
                    ConversionToBase = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitOfMeasures", x => x.UomId);
                    table.CheckConstraint("chk_uom_base_factor", "\"IsBaseUnit\" = false OR \"ConversionToBase\" = 1");
                    table.CheckConstraint("chk_uom_conversion_positive", "\"ConversionToBase\" > 0");
                    table.CheckConstraint("chk_uom_decimals", "\"DecimalPlaces\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_UnitOfMeasures_UomTypes_UomTypeId",
                        column: x => x.UomTypeId,
                        principalSchema: "inv",
                        principalTable: "UomTypes",
                        principalColumn: "UomTypeId",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "Items",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrintName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ItemProfile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ItemCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCodeId = table.Column<int>(type: "integer", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxPreference = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IsPriceInclusiveOfTax = table.Column<bool>(type: "boolean", nullable: false),
                    UomTypeId = table.Column<long>(type: "bigint", nullable: false),
                    InventoryUomId = table.Column<long>(type: "bigint", nullable: false),
                    SalesUomId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseUomId = table.Column<long>(type: "bigint", nullable: false),
                    ReportUomId = table.Column<long>(type: "bigint", nullable: false),
                    TrackInventory = table.Column<bool>(type: "boolean", nullable: false),
                    CostingType = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    IsBatchTracked = table.Column<bool>(type: "boolean", nullable: false),
                    IsExpiryTracked = table.Column<bool>(type: "boolean", nullable: false),
                    IsSerialTracked = table.Column<bool>(type: "boolean", nullable: false),
                    SalesPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Mrp = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MinSalePrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    StandardCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    MinStockLevel = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    MaxStockLevel = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    DefaultWarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsReturnable = table.Column<bool>(type: "boolean", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.ItemId);
                    table.CheckConstraint("chk_item_costing_tracking", "(\"TrackInventory\" = false AND \"CostingType\" = 'None') OR (\"TrackInventory\" = true AND \"CostingType\" <> 'None')");
                    table.CheckConstraint("chk_item_expiry_needs_batch", "\"IsExpiryTracked\" = false OR \"IsBatchTracked\" = true");
                    table.CheckConstraint("chk_item_fefo_tracking", "\"CostingType\" <> 'Fefo' OR (\"IsBatchTracked\" = true AND \"IsExpiryTracked\" = true)");
                    table.CheckConstraint("chk_item_min_sale_price", "\"MinSalePrice\" IS NULL OR \"SalesPrice\" IS NULL OR \"MinSalePrice\" <= \"SalesPrice\"");
                    table.CheckConstraint("chk_item_specific_tracking", "\"CostingType\" <> 'SpecificIdentification' OR \"IsSerialTracked\" = true");
                    table.ForeignKey(
                        name: "FK_Items_UomTypes_UomTypeId",
                        column: x => x.UomTypeId,
                        principalSchema: "inv",
                        principalTable: "UomTypes",
                        principalColumn: "UomTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Items_UnitOfMeasures_InventoryUomId",
                        column: x => x.InventoryUomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Items_ItemCategories_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalSchema: "inv",
                        principalTable: "ItemCategories",
                        principalColumn: "ItemCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "ItemJewelleryDetails",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    MetalType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MetalPurityId = table.Column<long>(type: "bigint", nullable: false),
                    GrossWeight = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    NetWeight = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    StoneWeight = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    StoneCharge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WastagePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MakingChargeType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    MakingChargeValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsHallmarked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemJewelleryDetails", x => x.ItemId);
                    table.CheckConstraint("chk_jewellery_making_percent", "\"MakingChargeType\" <> 'Percentage' OR \"MakingChargeValue\" <= 100");
                    table.CheckConstraint("chk_jewellery_weights", "\"NetWeight\" <= \"GrossWeight\" AND \"StoneWeight\" <= \"GrossWeight\"");
                    table.ForeignKey(
                        name: "FK_ItemJewelleryDetails_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemJewelleryDetails_MetalPurities_MetalPurityId",
                        column: x => x.MetalPurityId,
                        principalSchema: "inv",
                        principalTable: "MetalPurities",
                        principalColumn: "MetalPurityId",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "ItemPharmaDetails",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    GenericName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DosageForm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PackSize = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufacturerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MarketedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DrugSchedule = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsPrescriptionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsNarcotic = table.Column<bool>(type: "boolean", nullable: false),
                    StorageCondition = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    ShelfLifeDays = table.Column<int>(type: "integer", nullable: true),
                    MinExpiryDaysOnReceipt = table.Column<int>(type: "integer", nullable: false),
                    ExpiryAlertDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPharmaDetails", x => x.ItemId);
                    table.CheckConstraint("chk_pharma_prescription", "\"DrugSchedule\" NOT IN ('H', 'H1', 'X') OR \"IsPrescriptionRequired\" = true");
                    table.ForeignKey(
                        name: "FK_ItemPharmaDetails_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateTable(
                name: "ItemBarcodes",
                schema: "inv",
                columns: table => new
                {
                    ItemBarcodeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BarcodeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemBarcodes", x => x.ItemBarcodeId);
                    table.ForeignKey(
                        name: "FK_ItemBarcodes_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_OrgId",
                schema: "inv",
                table: "UomTypes",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_OrgId_UomTypeName",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "OrgId", "UomTypeName" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_SystemName",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "OrgId", "UomTypeSystemName" },
                unique: true,
                filter: "\"UomTypeSystemName\" IS NOT NULL");
            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_Order",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "OrgId", "DisplayOrder", "UomTypeName" });
            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_OrgId",
                schema: "inv",
                table: "ItemCategories",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_ParentCategoryId",
                schema: "inv",
                table: "ItemCategories",
                column: "ParentCategoryId");
            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_OrgId_CategoryCode",
                schema: "inv",
                table: "ItemCategories",
                columns: new[] { "OrgId", "CategoryCode" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_Order",
                schema: "inv",
                table: "ItemCategories",
                columns: new[] { "OrgId", "ParentCategoryId", "DisplayOrder", "CategoryName" });
            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_OrgId",
                schema: "inv",
                table: "MetalPurities",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_OrgId_MetalType_PurityName",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "OrgId", "MetalType", "PurityName" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_Order",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "OrgId", "MetalType", "DisplayOrder" });
            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_OrgId",
                schema: "inv",
                table: "Warehouses",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_OrgId_WarehouseCode",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "OrgId", "WarehouseCode" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Default",
                schema: "inv",
                table: "Warehouses",
                column: "OrgId",
                unique: true,
                filter: "\"IsDefault\" = true");
            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Order",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "OrgId", "DisplayOrder", "WarehouseName" });
            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_OrgId",
                schema: "inv",
                table: "UnitOfMeasures",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_UomTypeId",
                schema: "inv",
                table: "UnitOfMeasures",
                column: "UomTypeId");
            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_OrgId_UomCode",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomCode" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_BaseUnit",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomTypeId" },
                unique: true,
                filter: "\"IsBaseUnit\" = true");
            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_SystemName",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomSystemName" },
                unique: true,
                filter: "\"UomSystemName\" IS NOT NULL");
            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_Order",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomTypeId", "DisplayOrder", "UomCode" });
            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId",
                schema: "inv",
                table: "Items",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_Items_UomTypeId",
                schema: "inv",
                table: "Items",
                column: "UomTypeId");
            migrationBuilder.CreateIndex(
                name: "IX_Items_InventoryUomId",
                schema: "inv",
                table: "Items",
                column: "InventoryUomId");
            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemCategoryId",
                schema: "inv",
                table: "Items",
                column: "ItemCategoryId");
            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_ItemCode",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "ItemCode" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_ItemName",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "ItemName" });
            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_ItemCategoryId",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "ItemCategoryId" });
            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_HsnSacCodeId",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "HsnSacCodeId" });
            migrationBuilder.CreateIndex(
                name: "IX_Items_Order",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "DisplayOrder", "ItemName" });
            migrationBuilder.CreateIndex(
                name: "IX_Items_Sales",
                schema: "inv",
                table: "Items",
                column: "OrgId",
                filter: "\"IsSales\" = true");
            migrationBuilder.CreateIndex(
                name: "IX_Items_Purchase",
                schema: "inv",
                table: "Items",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");
            migrationBuilder.CreateIndex(
                name: "IX_ItemJewelleryDetails_OrgId",
                schema: "inv",
                table: "ItemJewelleryDetails",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_ItemJewelleryDetails_MetalPurityId",
                schema: "inv",
                table: "ItemJewelleryDetails",
                column: "MetalPurityId");
            migrationBuilder.CreateIndex(
                name: "IX_ItemPharmaDetails_OrgId",
                schema: "inv",
                table: "ItemPharmaDetails",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_ItemPharmaDetails_Generic",
                schema: "inv",
                table: "ItemPharmaDetails",
                columns: new[] { "OrgId", "GenericName" });
            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_OrgId",
                schema: "inv",
                table: "ItemBarcodes",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_ItemId",
                schema: "inv",
                table: "ItemBarcodes",
                column: "ItemId");
            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_OrgId_Barcode",
                schema: "inv",
                table: "ItemBarcodes",
                columns: new[] { "OrgId", "Barcode" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_OrgId_ItemId",
                schema: "inv",
                table: "ItemBarcodes",
                columns: new[] { "OrgId", "ItemId" });
            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_Primary",
                schema: "inv",
                table: "ItemBarcodes",
                columns: new[] { "OrgId", "ItemId" },
                unique: true,
                filter: "\"IsPrimary\" = true");
            // Row-level security on every inv table. The EF query filter is the
            // first line of defence; this is the one that holds if a query ever
            // runs without it.
            foreach (string table in new[]
                { "UomTypes", "ItemCategories", "MetalPurities", "Warehouses", "UnitOfMeasures", "Items", "ItemJewelleryDetails", "ItemPharmaDetails", "ItemBarcodes" })
            {
                migrationBuilder.Sql($"ALTER TABLE inv.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON inv.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
                { "UomTypes", "ItemCategories", "MetalPurities", "Warehouses", "UnitOfMeasures", "Items", "ItemJewelleryDetails", "ItemPharmaDetails", "ItemBarcodes" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON inv.\"{table}\";");
            }

            migrationBuilder.DropTable(name: "ItemBarcodes", schema: "inv");
            migrationBuilder.DropTable(name: "ItemPharmaDetails", schema: "inv");
            migrationBuilder.DropTable(name: "ItemJewelleryDetails", schema: "inv");
            migrationBuilder.DropTable(name: "Items", schema: "inv");
            migrationBuilder.DropTable(name: "UnitOfMeasures", schema: "inv");
            migrationBuilder.DropTable(name: "Warehouses", schema: "inv");
            migrationBuilder.DropTable(name: "MetalPurities", schema: "inv");
            migrationBuilder.DropTable(name: "ItemCategories", schema: "inv");
            migrationBuilder.DropTable(name: "UomTypes", schema: "inv");
        }
    }
}
