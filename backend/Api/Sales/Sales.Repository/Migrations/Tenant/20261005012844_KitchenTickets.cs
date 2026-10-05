using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class KitchenTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kots",
                schema: "sal",
                columns: table => new
                {
                    KotId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    KotNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NumberingSeriesId = table.Column<long>(type: "bigint", nullable: false),
                    Station = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrintedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Kots", x => x.KotId);
                    table.ForeignKey(
                        name: "FK_Kots_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KotLines",
                schema: "sal",
                columns: table => new
                {
                    KotLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KotId = table.Column<long>(type: "bigint", nullable: false),
                    RestaurantOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_KotLines", x => x.KotLineId);
                    table.ForeignKey(
                        name: "FK_KotLines_Kots_KotId",
                        column: x => x.KotId,
                        principalSchema: "sal",
                        principalTable: "Kots",
                        principalColumn: "KotId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotLines_RestaurantOrderLines_RestaurantOrderLineId",
                        column: x => x.RestaurantOrderLineId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrderLines",
                        principalColumn: "RestaurantOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KotLines_CustomerId_OrgId",
                schema: "sal",
                table: "KotLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_KotLines_KotId",
                schema: "sal",
                table: "KotLines",
                column: "KotId");

            migrationBuilder.CreateIndex(
                name: "IX_KotLines_RestaurantOrderLineId",
                schema: "sal",
                table: "KotLines",
                column: "RestaurantOrderLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kots_CustomerId_OrgId",
                schema: "sal",
                table: "Kots",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Kots_OrgId_KotNo",
                schema: "sal",
                table: "Kots",
                columns: new[] { "OrgId", "KotNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kots_OrgId_Station_SentAt",
                schema: "sal",
                table: "Kots",
                columns: new[] { "OrgId", "Station", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Kots_RestaurantOrderId",
                schema: "sal",
                table: "Kots",
                column: "RestaurantOrderId");

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "sal", "Kots");
            TenantRlsMigration.Enable(migrationBuilder, "sal", "KotLines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KotLines",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Kots",
                schema: "sal");
        }
    }
}
