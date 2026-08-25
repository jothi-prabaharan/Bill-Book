using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UomTypes_OrgId",
                schema: "inv",
                table: "UomTypes");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasures_OrgId",
                schema: "inv",
                table: "UnitOfMeasures");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OrgId",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_OrgId",
                schema: "inv",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustmentLines_OrgId",
                schema: "inv",
                table: "StockAdjustmentLines");

            migrationBuilder.DropIndex(
                name: "IX_RecostingAdjustments_OrgId",
                schema: "inv",
                table: "RecostingAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PriceLists_OrgId",
                schema: "inv",
                table: "PriceLists");

            migrationBuilder.DropIndex(
                name: "IX_PriceListItems_OrgId",
                schema: "inv",
                table: "PriceListItems");

            migrationBuilder.DropIndex(
                name: "IX_MetalPurities_OrgId",
                schema: "inv",
                table: "MetalPurities");

            migrationBuilder.DropIndex(
                name: "IX_Items_OrgId",
                schema: "inv",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ItemStock_OrgId",
                schema: "inv",
                table: "ItemStock");

            migrationBuilder.DropIndex(
                name: "IX_ItemSerials_OrgId",
                schema: "inv",
                table: "ItemSerials");

            migrationBuilder.DropIndex(
                name: "IX_ItemPharmaDetails_OrgId",
                schema: "inv",
                table: "ItemPharmaDetails");

            migrationBuilder.DropIndex(
                name: "IX_ItemJewelleryDetails_OrgId",
                schema: "inv",
                table: "ItemJewelleryDetails");

            migrationBuilder.DropIndex(
                name: "IX_ItemCategories_OrgId",
                schema: "inv",
                table: "ItemCategories");

            migrationBuilder.DropIndex(
                name: "IX_ItemBatches_OrgId",
                schema: "inv",
                table: "ItemBatches");

            migrationBuilder.DropIndex(
                name: "IX_ItemBarcodes_OrgId",
                schema: "inv",
                table: "ItemBarcodes");

            migrationBuilder.DropIndex(
                name: "IX_CostLayers_OrgId",
                schema: "inv",
                table: "CostLayers");

            migrationBuilder.DropIndex(
                name: "IX_CostLayerConsumptions_OrgId",
                schema: "inv",
                table: "CostLayerConsumptions");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "Warehouses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "UomTypes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "UnitOfMeasures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "StockMovements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "StockAdjustments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "StockAdjustmentLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "RecostingAdjustments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "PriceLists",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "PriceListItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "MetalPurities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "Items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "ItemStock",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "ItemSerials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "ItemPharmaDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "ItemJewelleryDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "ItemCategories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "ItemBatches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "ItemBarcodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "CostLayers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "inv",
                table: "CostLayerConsumptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CustomerId_OrgId",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_CustomerId_OrgId",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_CustomerId_OrgId",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CustomerId_OrgId",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_CustomerId_OrgId",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_CustomerId_OrgId",
                schema: "inv",
                table: "StockAdjustmentLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_CustomerId_OrgId",
                schema: "inv",
                table: "RecostingAdjustments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_CustomerId_OrgId",
                schema: "inv",
                table: "PriceLists",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_CustomerId_OrgId",
                schema: "inv",
                table: "PriceListItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_CustomerId_OrgId",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_CustomerId_OrgId",
                schema: "inv",
                table: "Items",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemStock_CustomerId_OrgId",
                schema: "inv",
                table: "ItemStock",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_CustomerId_OrgId",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemPharmaDetails_CustomerId_OrgId",
                schema: "inv",
                table: "ItemPharmaDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemJewelleryDetails_CustomerId_OrgId",
                schema: "inv",
                table: "ItemJewelleryDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_CustomerId_OrgId",
                schema: "inv",
                table: "ItemCategories",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_CustomerId_OrgId",
                schema: "inv",
                table: "ItemBatches",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_CustomerId_OrgId",
                schema: "inv",
                table: "ItemBarcodes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_CustomerId_OrgId",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_CustomerId_OrgId",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_CustomerId_OrgId",
                schema: "inv",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_UomTypes_CustomerId_OrgId",
                schema: "inv",
                table: "UomTypes");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasures_CustomerId_OrgId",
                schema: "inv",
                table: "UnitOfMeasures");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_CustomerId_OrgId",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_CustomerId_OrgId",
                schema: "inv",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustmentLines_CustomerId_OrgId",
                schema: "inv",
                table: "StockAdjustmentLines");

            migrationBuilder.DropIndex(
                name: "IX_RecostingAdjustments_CustomerId_OrgId",
                schema: "inv",
                table: "RecostingAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PriceLists_CustomerId_OrgId",
                schema: "inv",
                table: "PriceLists");

            migrationBuilder.DropIndex(
                name: "IX_PriceListItems_CustomerId_OrgId",
                schema: "inv",
                table: "PriceListItems");

            migrationBuilder.DropIndex(
                name: "IX_MetalPurities_CustomerId_OrgId",
                schema: "inv",
                table: "MetalPurities");

            migrationBuilder.DropIndex(
                name: "IX_Items_CustomerId_OrgId",
                schema: "inv",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ItemStock_CustomerId_OrgId",
                schema: "inv",
                table: "ItemStock");

            migrationBuilder.DropIndex(
                name: "IX_ItemSerials_CustomerId_OrgId",
                schema: "inv",
                table: "ItemSerials");

            migrationBuilder.DropIndex(
                name: "IX_ItemPharmaDetails_CustomerId_OrgId",
                schema: "inv",
                table: "ItemPharmaDetails");

            migrationBuilder.DropIndex(
                name: "IX_ItemJewelleryDetails_CustomerId_OrgId",
                schema: "inv",
                table: "ItemJewelleryDetails");

            migrationBuilder.DropIndex(
                name: "IX_ItemCategories_CustomerId_OrgId",
                schema: "inv",
                table: "ItemCategories");

            migrationBuilder.DropIndex(
                name: "IX_ItemBatches_CustomerId_OrgId",
                schema: "inv",
                table: "ItemBatches");

            migrationBuilder.DropIndex(
                name: "IX_ItemBarcodes_CustomerId_OrgId",
                schema: "inv",
                table: "ItemBarcodes");

            migrationBuilder.DropIndex(
                name: "IX_CostLayers_CustomerId_OrgId",
                schema: "inv",
                table: "CostLayers");

            migrationBuilder.DropIndex(
                name: "IX_CostLayerConsumptions_CustomerId_OrgId",
                schema: "inv",
                table: "CostLayerConsumptions");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "UomTypes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "UnitOfMeasures");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "StockAdjustmentLines");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "RecostingAdjustments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "PriceLists");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "PriceListItems");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "MetalPurities");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "ItemStock");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "ItemSerials");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "ItemPharmaDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "ItemJewelleryDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "ItemCategories");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "ItemBatches");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "ItemBarcodes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "CostLayers");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "inv",
                table: "CostLayerConsumptions");

            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_OrgId",
                schema: "inv",
                table: "UomTypes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_OrgId",
                schema: "inv",
                table: "UnitOfMeasures",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrgId",
                schema: "inv",
                table: "StockMovements",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_OrgId",
                schema: "inv",
                table: "StockAdjustments",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_OrgId",
                schema: "inv",
                table: "StockAdjustmentLines",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_OrgId",
                schema: "inv",
                table: "RecostingAdjustments",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_OrgId",
                schema: "inv",
                table: "PriceLists",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_OrgId",
                schema: "inv",
                table: "PriceListItems",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_OrgId",
                schema: "inv",
                table: "MetalPurities",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId",
                schema: "inv",
                table: "Items",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemStock_OrgId",
                schema: "inv",
                table: "ItemStock",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_OrgId",
                schema: "inv",
                table: "ItemSerials",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPharmaDetails_OrgId",
                schema: "inv",
                table: "ItemPharmaDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemJewelleryDetails_OrgId",
                schema: "inv",
                table: "ItemJewelleryDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_OrgId",
                schema: "inv",
                table: "ItemCategories",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_OrgId",
                schema: "inv",
                table: "ItemBatches",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_OrgId",
                schema: "inv",
                table: "ItemBarcodes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_OrgId",
                schema: "inv",
                table: "CostLayers",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_OrgId",
                schema: "inv",
                table: "CostLayerConsumptions",
                column: "OrgId");
        }
    }
}
