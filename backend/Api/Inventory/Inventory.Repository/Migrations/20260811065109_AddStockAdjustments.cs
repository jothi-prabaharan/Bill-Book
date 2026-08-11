using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                schema: "inv",
                columns: table => new
                {
                    StockAdjustmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdjustmentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AdjustmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedByStockAdjustmentId = table.Column<long>(type: "bigint", nullable: true),
                    ReversesStockAdjustmentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.StockAdjustmentId);
                    table.CheckConstraint("chk_adjustment_not_self_reversing", "\"ReversesStockAdjustmentId\" IS NULL OR \"ReversesStockAdjustmentId\" <> \"StockAdjustmentId\"");
                    table.CheckConstraint("chk_adjustment_posted", "(\"Status\" = 'Draft' AND \"AdjustmentNo\" IS NULL AND \"PostedAt\" IS NULL) OR (\"Status\" <> 'Draft' AND \"AdjustmentNo\" IS NOT NULL AND \"PostedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StockAdjustments_StockAdjustments_ReversesStockAdjustmentId",
                        column: x => x.ReversesStockAdjustmentId,
                        principalSchema: "inv",
                        principalTable: "StockAdjustments",
                        principalColumn: "StockAdjustmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inv",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentLines",
                schema: "inv",
                columns: table => new
                {
                    StockAdjustmentLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockAdjustmentId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    SystemQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Direction = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BatchExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_StockAdjustmentLines", x => x.StockAdjustmentLineId);
                    table.CheckConstraint("chk_adjustment_line_cost", "\"UnitCost\" IS NULL OR \"Direction\" = 'In'");
                    table.CheckConstraint("chk_adjustment_line_count", "(\"CountedQuantity\" IS NULL AND \"SystemQuantity\" IS NULL) OR (\"CountedQuantity\" IS NOT NULL AND \"SystemQuantity\" IS NOT NULL)");
                    table.CheckConstraint("chk_adjustment_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalSchema: "inv",
                        principalTable: "StockAdjustments",
                        principalColumn: "StockAdjustmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_Item",
                schema: "inv",
                table: "StockAdjustmentLines",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_ItemId",
                schema: "inv",
                table: "StockAdjustmentLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_Line",
                schema: "inv",
                table: "StockAdjustmentLines",
                columns: new[] { "StockAdjustmentId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_OrgId",
                schema: "inv",
                table: "StockAdjustmentLines",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_StockMovementId",
                schema: "inv",
                table: "StockAdjustmentLines",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Date",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "OrgId", "AdjustmentDate", "StockAdjustmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Number",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "OrgId", "AdjustmentNo" },
                unique: true,
                filter: "\"AdjustmentNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_OrgId",
                schema: "inv",
                table: "StockAdjustments",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ReversesStockAdjustmentId",
                schema: "inv",
                table: "StockAdjustments",
                column: "ReversesStockAdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Status",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_WarehouseId",
                schema: "inv",
                table: "StockAdjustments",
                column: "WarehouseId");

            // Row-level security, on both tables including the detail. The EF
            // query filter is the first line of defence, not the last: it is a
            // property of the code, and one query written with
            // IgnoreQueryFilters — the seeders use it legitimately — or a
            // context built without a tenant would read another branch's stock.
            // The policy is a property of the database and holds however the
            // connection is used.
            foreach ((string table, string policy) in new[]
            {
                ("StockAdjustments", "stockadjustments_org_isolation"),
                ("StockAdjustmentLines", "stockadjustmentlines_org_isolation"),
            })
            {
                migrationBuilder.Sql($"ALTER TABLE inv.\"{table}\" ENABLE ROW LEVEL SECURITY;");

                // Dropped first so the migration is safe to re-run against a
                // database where it was applied by hand.
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {policy} ON inv.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {policy} ON inv.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach ((string table, string policy) in new[]
            {
                ("StockAdjustmentLines", "stockadjustmentlines_org_isolation"),
                ("StockAdjustments", "stockadjustments_org_isolation"),
            })
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {policy} ON inv.\"{table}\";");
            }

            migrationBuilder.DropTable(
                name: "StockAdjustmentLines",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "StockAdjustments",
                schema: "inv");
        }
    }
}
