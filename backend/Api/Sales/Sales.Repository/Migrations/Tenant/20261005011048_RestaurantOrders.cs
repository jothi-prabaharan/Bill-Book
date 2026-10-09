using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RestaurantOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestaurantOrders",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NumberingSeriesId = table.Column<long>(type: "bigint", nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: true),
                    WaiterUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    Guests = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    MergedIntoOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrders", x => x.RestaurantOrderId);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_RestaurantOrders_MergedIntoOrderId",
                        column: x => x.MergedIntoOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_RestaurantTables_RestaurantTableId",
                        column: x => x.RestaurantTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrderLines",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemVariantId = table.Column<long>(type: "bigint", nullable: true),
                    VariantName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PortionFactor = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    Station = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    KitchenStatus = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrderLines", x => x.RestaurantOrderLineId);
                    table.CheckConstraint("chk_restaurant_order_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrderLineModifiers",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderLineModifierId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    ModifierId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceChange = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LinkedItemId = table.Column<long>(type: "bigint", nullable: true),
                    LinkedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrderLineModifiers", x => x.RestaurantOrderLineModifierId);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLineModifiers_RestaurantOrderLines_Restauran~",
                        column: x => x.RestaurantOrderLineId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrderLines",
                        principalColumn: "RestaurantOrderLineId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLineModifiers_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrderLineModifiers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLineModifiers_RestaurantOrderLineId",
                schema: "sal",
                table: "RestaurantOrderLineModifiers",
                column: "RestaurantOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrderLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_OrgId_KitchenStatus_Station",
                schema: "sal",
                table: "RestaurantOrderLines",
                columns: new[] { "OrgId", "KitchenStatus", "Station" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_RestaurantOrderId",
                schema: "sal",
                table: "RestaurantOrderLines",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_MergedIntoOrderId",
                schema: "sal",
                table: "RestaurantOrders",
                column: "MergedIntoOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OrgId_OrderNo",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "OrgId", "OrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OrgId_RestaurantTableId",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "OrgId", "RestaurantTableId" },
                unique: true,
                filter: "\"Status\" = 'Open' AND \"RestaurantTableId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OrgId_Status_OpenedAt",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "OrgId", "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_RestaurantTableId",
                schema: "sal",
                table: "RestaurantOrders",
                column: "RestaurantTableId");

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "sal", "RestaurantOrders");
            TenantRlsMigration.Enable(migrationBuilder, "sal", "RestaurantOrderLines");
            TenantRlsMigration.Enable(migrationBuilder, "sal", "RestaurantOrderLineModifiers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestaurantOrderLineModifiers",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantOrderLines",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantOrders",
                schema: "sal");
        }
    }
}
