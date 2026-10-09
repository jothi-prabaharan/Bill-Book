using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RestaurantTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Floors",
                schema: "sal",
                columns: table => new
                {
                    FloorId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Floors", x => x.FloorId);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                schema: "sal",
                columns: table => new
                {
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FloorId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MergedIntoTableId = table.Column<long>(type: "bigint", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_RestaurantTables", x => x.RestaurantTableId);
                    table.CheckConstraint("chk_restaurant_table_not_self", "\"MergedIntoTableId\" IS NULL OR \"MergedIntoTableId\" <> \"RestaurantTableId\"");
                    table.CheckConstraint("chk_restaurant_table_seats", "\"Seats\" BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_RestaurantTables_Floors_FloorId",
                        column: x => x.FloorId,
                        principalSchema: "sal",
                        principalTable: "Floors",
                        principalColumn: "FloorId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantTables_RestaurantTables_MergedIntoTableId",
                        column: x => x.MergedIntoTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Floors_CustomerId_OrgId",
                schema: "sal",
                table: "Floors",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Floors_OrgId_Name",
                schema: "sal",
                table: "Floors",
                columns: new[] { "OrgId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantTables",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_FloorId",
                schema: "sal",
                table: "RestaurantTables",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_MergedIntoTableId",
                schema: "sal",
                table: "RestaurantTables",
                column: "MergedIntoTableId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_OrgId_Code",
                schema: "sal",
                table: "RestaurantTables",
                columns: new[] { "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_OrgId_FloorId_DisplayOrder",
                schema: "sal",
                table: "RestaurantTables",
                columns: new[] { "OrgId", "FloorId", "DisplayOrder" });

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "sal", "Floors");
            TenantRlsMigration.Enable(migrationBuilder, "sal", "RestaurantTables");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestaurantTables",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Floors",
                schema: "sal");
        }
    }
}
