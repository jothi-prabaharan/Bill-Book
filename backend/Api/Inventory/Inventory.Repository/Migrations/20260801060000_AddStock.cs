using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemStock",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    WeightedAverageCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    LastMovementAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemStock", x => x.ItemId);
                    table.CheckConstraint(
                        "chk_item_stock_non_negative",
                        "\"QuantityOnHand\" >= 0 AND \"WeightedAverageCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_ItemStock_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "inv",
                columns: table => new
                {
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    MovementType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Direction = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EnteredQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EnteredUomId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ResultingWeightedAverageCost = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    SourceLineId = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.StockMovementId);
                    table.CheckConstraint(
                        "chk_movement_conversion",
                        "\"Quantity\" = ROUND(\"EnteredQuantity\" * \"ConversionFactor\", 3)");
                    table.CheckConstraint(
                        "chk_movement_cost_non_negative",
                        "(\"UnitCost\" IS NULL OR \"UnitCost\" >= 0) AND (\"TotalCost\" IS NULL OR \"TotalCost\" >= 0)");
                    table.CheckConstraint(
                        "chk_movement_quantity_positive",
                        "\"Quantity\" > 0 AND \"EnteredQuantity\" > 0 AND \"ConversionFactor\" > 0");
                    table.CheckConstraint(
                        "chk_movement_source",
                        "(\"SourceType\" IS NULL AND \"SourceId\" IS NULL) OR (\"SourceType\" IS NOT NULL AND \"SourceId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StockMovements_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_UnitOfMeasures_EnteredUomId",
                        column: x => x.EnteredUomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inv",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemStock_OrgId",
                schema: "inv",
                table: "ItemStock",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrgId",
                schema: "inv",
                table: "StockMovements",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_EnteredUomId",
                schema: "inv",
                table: "StockMovements",
                column: "EnteredUomId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId",
                schema: "inv",
                table: "StockMovements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId",
                schema: "inv",
                table: "StockMovements",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Item",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "ItemId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Warehouse",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "WarehouseId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Date",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "MovementDate" });

            // The idempotency key. Service Bus is at-least-once, so a
            // redelivered event has to fail here rather than move stock twice.
            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Source",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "SourceType", "SourceId", "SourceLineId" },
                unique: true,
                filter: "\"SourceType\" IS NOT NULL");

            foreach (string table in new[] { "ItemStock", "StockMovements" })
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
            foreach (string table in new[] { "StockMovements", "ItemStock" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON inv.\"{table}\";");
            }

            migrationBuilder.DropTable(name: "StockMovements", schema: "inv");
            migrationBuilder.DropTable(name: "ItemStock", schema: "inv");
        }
    }
}
