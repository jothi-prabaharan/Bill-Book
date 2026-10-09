using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AggregatorOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AggregatorOrders",
                schema: "sal",
                columns: table => new
                {
                    AggregatorOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Aggregator = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExternalOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AggregatorTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_AggregatorOrders", x => x.AggregatorOrderId);
                    table.ForeignKey(
                        name: "FK_AggregatorOrders_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_CustomerId_OrgId",
                schema: "sal",
                table: "AggregatorOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_OrgId_Aggregator_ExternalOrderId",
                schema: "sal",
                table: "AggregatorOrders",
                columns: new[] { "OrgId", "Aggregator", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_OrgId_ReceivedAt",
                schema: "sal",
                table: "AggregatorOrders",
                columns: new[] { "OrgId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_RestaurantOrderId",
                schema: "sal",
                table: "AggregatorOrders",
                column: "RestaurantOrderId",
                unique: true);

            // RLS in TK-71's form, after the table exists (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "sal", "AggregatorOrders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AggregatorOrders",
                schema: "sal");
        }
    }
}
