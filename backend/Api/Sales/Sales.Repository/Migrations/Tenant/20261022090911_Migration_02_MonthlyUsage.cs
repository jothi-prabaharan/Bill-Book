using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Shared.Kernel.Persistence;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_02_MonthlyUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyUsages",
                schema: "sal",
                columns: table => new
                {
                    MonthlyUsageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_MonthlyUsages", x => x.MonthlyUsageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyUsages_CustomerId_OrgId",
                schema: "sal",
                table: "MonthlyUsages",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyUsages_OrgId_PeriodStart",
                schema: "sal",
                table: "MonthlyUsages",
                columns: new[] { "OrgId", "PeriodStart" },
                unique: true);

            // The tenant policy, in the form every sal table has: enabled, FORCEd, on
            // CustomerId and OrgId. The model knows nothing of it, so a regenerated
            // migration would drop it (TK-71).
            TenantRlsMigration.Enable(migrationBuilder, "sal", "MonthlyUsages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyUsages",
                schema: "sal");
        }
    }
}
