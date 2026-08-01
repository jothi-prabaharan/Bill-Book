using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <summary>
    /// Backdated receipts restate the sales that came after them. Allocations
    /// are superseded rather than deleted, and the difference each restatement
    /// made is recorded so an accountant can see which sale changed, by how
    /// much, and because of which receipt.
    /// </summary>
    public partial class AddRecosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The unique index has to admit a replacement alongside what it
            // replaced, so it is rebuilt over current allocations only.
            migrationBuilder.DropIndex(
                name: "IX_CostLayerConsumptions_Allocation",
                schema: "inv",
                table: "CostLayerConsumptions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SupersededAt",
                schema: "inv",
                table: "CostLayerConsumptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecostingBatchId",
                schema: "inv",
                table: "CostLayerConsumptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Allocation",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "StockMovementId", "CostLayerId" },
                unique: true,
                filter: "\"SupersededAt\" IS NULL");

            migrationBuilder.CreateTable(
                name: "RecostingAdjustments",
                schema: "inv",
                columns: table => new
                {
                    RecostingAdjustmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecostingBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    TriggerStockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecostingAdjustments", x => x.RecostingAdjustmentId);
                    table.CheckConstraint(
                        "chk_recosting_delta",
                        "\"Delta\" = \"NewCost\" - \"PreviousCost\"");
                    table.ForeignKey(
                        name: "FK_RecostingAdjustments_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecostingAdjustments_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            foreach ((string name, string[] columns) in new (string, string[])[]
            {
                ("IX_RecostingAdjustments_OrgId", new[] { "OrgId" }),
                ("IX_RecostingAdjustments_ItemId", new[] { "ItemId" }),
                ("IX_RecostingAdjustments_StockMovementId", new[] { "StockMovementId" }),
                ("IX_RecostingAdjustments_Batch", new[] { "OrgId", "RecostingBatchId" }),
                ("IX_RecostingAdjustments_Item", new[] { "OrgId", "ItemId", "RunAt" }),
                ("IX_RecostingAdjustments_Movement", new[] { "OrgId", "StockMovementId" }),
            })
            {
                migrationBuilder.CreateIndex(
                    name: name, schema: "inv", table: "RecostingAdjustments", columns: columns);
            }

            migrationBuilder.Sql("ALTER TABLE inv.\"RecostingAdjustments\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "CREATE POLICY recostingadjustments_org_isolation ON inv.\"RecostingAdjustments\" " +
                "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS recostingadjustments_org_isolation ON inv.\"RecostingAdjustments\";");

            migrationBuilder.DropTable(name: "RecostingAdjustments", schema: "inv");

            migrationBuilder.DropIndex(
                name: "IX_CostLayerConsumptions_Allocation",
                schema: "inv",
                table: "CostLayerConsumptions");

            migrationBuilder.DropColumn(
                name: "RecostingBatchId", schema: "inv", table: "CostLayerConsumptions");

            migrationBuilder.DropColumn(
                name: "SupersededAt", schema: "inv", table: "CostLayerConsumptions");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Allocation",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "StockMovementId", "CostLayerId" },
                unique: true);
        }
    }
}
