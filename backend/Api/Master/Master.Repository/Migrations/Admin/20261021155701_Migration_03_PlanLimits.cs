using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class Migration_03_PlanLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MaxUsers",
                schema: "mst",
                table: "Licenses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "MonthlyPurchaseTransactionLimit",
                schema: "mst",
                table: "Licenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlySalesTransactionLimit",
                schema: "mst",
                table: "Licenses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UpgradeRequests",
                schema: "mst",
                columns: table => new
                {
                    UpgradeRequestId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPlan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToPlan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpgradeRequests", x => x.UpgradeRequestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_CustomerId_Status",
                schema: "mst",
                table: "UpgradeRequests",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeRequests_Status_CreatedAt",
                schema: "mst",
                table: "UpgradeRequests",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpgradeRequests",
                schema: "mst");

            migrationBuilder.DropColumn(
                name: "MonthlyPurchaseTransactionLimit",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MonthlySalesTransactionLimit",
                schema: "mst",
                table: "Licenses");

            migrationBuilder.AlterColumn<int>(
                name: "MaxUsers",
                schema: "mst",
                table: "Licenses",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
