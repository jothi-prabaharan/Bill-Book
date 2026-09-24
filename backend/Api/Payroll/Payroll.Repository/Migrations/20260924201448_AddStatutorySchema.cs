using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Payroll.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BonusSettings",
                schema: "pay",
                columns: table => new
                {
                    BonusSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MinBonusPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxBonusPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    CalculationCeiling = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_BonusSettings", x => x.BonusSettingId);
                });

            migrationBuilder.CreateTable(
                name: "EsiSettings",
                schema: "pay",
                columns: table => new
                {
                    EsiSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    WageCeiling = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_EsiSettings", x => x.EsiSettingId);
                });

            migrationBuilder.CreateTable(
                name: "GratuitySettings",
                schema: "pay",
                columns: table => new
                {
                    GratuitySettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    GratuityPercentage = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_GratuitySettings", x => x.GratuitySettingId);
                });

            migrationBuilder.CreateTable(
                name: "LwfSettings",
                schema: "pay",
                columns: table => new
                {
                    LwfSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeContribution = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerContribution = table.Column<decimal>(type: "numeric", nullable: false),
                    DeductionFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_LwfSettings", x => x.LwfSettingId);
                });

            migrationBuilder.CreateTable(
                name: "PfSettings",
                schema: "pay",
                columns: table => new
                {
                    PfSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    WageCeiling = table.Column<decimal>(type: "numeric", nullable: false),
                    RestrictToWageCeiling = table.Column<bool>(type: "boolean", nullable: false),
                    AdminChargesRate = table.Column<decimal>(type: "numeric", nullable: false),
                    EdliRate = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_PfSettings", x => x.PfSettingId);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalTaxSlabs",
                schema: "pay",
                columns: table => new
                {
                    ProfessionalTaxSlabId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MinSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    MonthlyTax = table.Column<decimal>(type: "numeric", nullable: false),
                    Month12Tax = table.Column<decimal>(type: "numeric", nullable: true),
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
                    table.PrimaryKey("PK_ProfessionalTaxSlabs", x => x.ProfessionalTaxSlabId);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryReturns",
                schema: "pay",
                columns: table => new
                {
                    StatutoryReturnId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileContent = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_StatutoryReturns", x => x.StatutoryReturnId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BonusSettings_CustomerId_OrgId",
                schema: "pay",
                table: "BonusSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EsiSettings_CustomerId_OrgId",
                schema: "pay",
                table: "EsiSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GratuitySettings_CustomerId_OrgId",
                schema: "pay",
                table: "GratuitySettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LwfSettings_CustomerId_OrgId",
                schema: "pay",
                table: "LwfSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PfSettings_CustomerId_OrgId",
                schema: "pay",
                table: "PfSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalTaxSlabs_CustomerId_OrgId",
                schema: "pay",
                table: "ProfessionalTaxSlabs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryReturns_CustomerId_OrgId",
                schema: "pay",
                table: "StatutoryReturns",
                columns: new[] { "CustomerId", "OrgId" });

            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE pay."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE pay."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON pay."{table}";
                    CREATE POLICY {policy}
                        ON pay."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in Tables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON pay."{table}";
                    ALTER TABLE pay."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE pay."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }

            migrationBuilder.DropTable(
                name: "BonusSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "EsiSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "GratuitySettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "LwfSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "PfSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "ProfessionalTaxSlabs",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "StatutoryReturns",
                schema: "pay");
        }

        private static readonly string[] Tables =
        [
            "BonusSettings",
            "EsiSettings",
            "GratuitySettings",
            "LwfSettings",
            "PfSettings",
            "ProfessionalTaxSlabs",
            "StatutoryReturns"
        ];
    }
}
