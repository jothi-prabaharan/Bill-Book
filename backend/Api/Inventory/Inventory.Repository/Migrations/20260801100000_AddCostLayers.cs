using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <summary>
    /// Batches, serials, and the cost layers that make FIFO, LIFO, FEFO and
    /// specific identification mean something. Until this, every item costed at
    /// weighted average whatever its method said.
    /// </summary>
    public partial class AddCostLayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemBatches",
                schema: "inv",
                columns: table => new
                {
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Mrp = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SupplierBatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_ItemBatches", x => x.ItemBatchId);
                    table.CheckConstraint(
                        "chk_batch_dates",
                        "\"ManufactureDate\" IS NULL OR \"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"ManufactureDate\"");
                    table.ForeignKey(
                        name: "FK_ItemBatches_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostLayers",
                schema: "inv",
                columns: table => new
                {
                    CostLayerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostLayers", x => x.CostLayerId);
                    table.CheckConstraint(
                        "chk_layer_remaining",
                        "\"RemainingQuantity\" >= 0 AND \"RemainingQuantity\" <= \"OriginalQuantity\" AND \"OriginalQuantity\" > 0 AND \"UnitCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_CostLayers_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostLayers_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalSchema: "inv",
                        principalTable: "ItemBatches",
                        principalColumn: "ItemBatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostLayers_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemSerials",
                schema: "inv",
                columns: table => new
                {
                    ItemSerialId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    HallmarkNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    CostLayerId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    ReceivedMovementId = table.Column<long>(type: "bigint", nullable: true),
                    IssuedMovementId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSerials", x => x.ItemSerialId);
                    table.ForeignKey(
                        name: "FK_ItemSerials_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemSerials_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalSchema: "inv",
                        principalTable: "ItemBatches",
                        principalColumn: "ItemBatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostLayerConsumptions",
                schema: "inv",
                columns: table => new
                {
                    CostLayerConsumptionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostLayerId = table.Column<long>(type: "bigint", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostLayerConsumptions", x => x.CostLayerConsumptionId);
                    table.CheckConstraint(
                        "chk_consumption_amounts",
                        "\"Quantity\" > 0 AND \"UnitCost\" >= 0 AND \"TotalCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_CostLayerConsumptions_CostLayers_CostLayerId",
                        column: x => x.CostLayerId,
                        principalSchema: "inv",
                        principalTable: "CostLayers",
                        principalColumn: "CostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostLayerConsumptions_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            foreach ((string table, string column) in new[]
            {
                ("ItemBatches", "OrgId"),
                ("ItemBatches", "ItemId"),
                ("ItemSerials", "OrgId"),
                ("ItemSerials", "ItemId"),
                ("ItemSerials", "ItemBatchId"),
                ("CostLayers", "OrgId"),
                ("CostLayers", "ItemId"),
                ("CostLayers", "ItemBatchId"),
                ("CostLayers", "StockMovementId"),
                ("CostLayerConsumptions", "OrgId"),
                ("CostLayerConsumptions", "CostLayerId"),
                ("CostLayerConsumptions", "StockMovementId"),
            })
            {
                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_{column}", schema: "inv", table: table, column: column);
            }

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_OrgId_ItemId_BatchNumber",
                schema: "inv",
                table: "ItemBatches",
                columns: new[] { "OrgId", "ItemId", "BatchNumber" },
                unique: true);

            // What FEFO orders by.
            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_Expiry",
                schema: "inv",
                table: "ItemBatches",
                columns: new[] { "OrgId", "ItemId", "ExpiryDate" },
                filter: "\"ExpiryDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_OrgId_ItemId_SerialNumber",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "OrgId", "ItemId", "SerialNumber" },
                unique: true);

            // A HUID identifies exactly one physical piece.
            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_Huid",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "OrgId", "HallmarkNumber" },
                unique: true,
                filter: "\"HallmarkNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_Status",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "OrgId", "ItemId", "Status" });

            // One layer per inbound movement.
            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_OrgId_StockMovementId",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "OrgId", "StockMovementId" },
                unique: true);

            // FIFO ascending, LIFO descending — one index serves both.
            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_Fifo",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "OrgId", "ItemId", "ReceivedOn" },
                filter: "\"RemainingQuantity\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_Fefo",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "OrgId", "ItemId", "ExpiresOn" },
                filter: "\"RemainingQuantity\" > 0 AND \"ExpiresOn\" IS NOT NULL");

            // One allocation per layer per issue — the idempotency key.
            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Allocation",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "StockMovementId", "CostLayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Movement",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "StockMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Layer",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "CostLayerId" });

            foreach (string table in new[]
                { "ItemBatches", "ItemSerials", "CostLayers", "CostLayerConsumptions" })
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
                { "CostLayerConsumptions", "CostLayers", "ItemSerials", "ItemBatches" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON inv.\"{table}\";");

                migrationBuilder.DropTable(name: table, schema: "inv");
            }
        }
    }
}
