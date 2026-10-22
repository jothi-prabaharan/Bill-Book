using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class Migration_04_PlanPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanPrices",
                schema: "mst",
                columns: table => new
                {
                    PlanPriceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BillingCycle = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanPrices", x => x.PlanPriceId);
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "AddOns", "BusinessType", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[] { 1156, 0, null, false, "plan", null, null, 11, "gem", true, false, null, null, "settings", "Plan", 114, "/settings/plan", null, "Item" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 523, "view", null, null, 1156, null, null, "settings", "settings.view" },
                    { 524, "edit", null, null, 1156, null, null, "settings", "settings.edit" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanPrices_BusinessType_Plan_BillingCycle",
                schema: "mst",
                table: "PlanPrices",
                columns: new[] { "BusinessType", "Plan", "BillingCycle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanPrices",
                schema: "mst");

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 523);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 524);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1156);
        }
    }
}
