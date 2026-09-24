using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Payroll.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeTaxSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreviousEmployerIncomes",
                schema: "pay",
                columns: table => new
                {
                    PreviousEmployerIncomeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GrossIncome = table.Column<decimal>(type: "numeric", nullable: false),
                    Exemptions = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfessionalTax = table.Column<decimal>(type: "numeric", nullable: false),
                    ProvidentFund = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTdsDeducted = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_PreviousEmployerIncomes", x => x.PreviousEmployerIncomeId);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarations",
                schema: "pay",
                columns: table => new
                {
                    TaxDeclarationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_TaxDeclarations", x => x.TaxDeclarationId);
                });

            migrationBuilder.CreateTable(
                name: "TaxRules",
                schema: "pay",
                columns: table => new
                {
                    TaxRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Section = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxLimit = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_TaxRules", x => x.TaxRuleId);
                });

            migrationBuilder.CreateTable(
                name: "TaxSlabs",
                schema: "pay",
                columns: table => new
                {
                    TaxSlabId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MinIncome = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxIncome = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric", nullable: false),
                    CessRate = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_TaxSlabs", x => x.TaxSlabId);
                });

            migrationBuilder.CreateTable(
                name: "RentDetails",
                schema: "pay",
                columns: table => new
                {
                    RentDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxDeclarationId = table.Column<long>(type: "bigint", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    RentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    LandlordPan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LandlordName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LandlordAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_RentDetails", x => x.RentDetailId);
                    table.ForeignKey(
                        name: "FK_RentDetails_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalSchema: "pay",
                        principalTable: "TaxDeclarations",
                        principalColumn: "TaxDeclarationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarationLines",
                schema: "pay",
                columns: table => new
                {
                    TaxDeclarationLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxDeclarationId = table.Column<long>(type: "bigint", nullable: false),
                    Section = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeclaredAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ProofAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    VerifiedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_TaxDeclarationLines", x => x.TaxDeclarationLineId);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationLines_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalSchema: "pay",
                        principalTable: "TaxDeclarations",
                        principalColumn: "TaxDeclarationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreviousEmployerIncomes_CustomerId_OrgId",
                schema: "pay",
                table: "PreviousEmployerIncomes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RentDetails_CustomerId_OrgId",
                schema: "pay",
                table: "RentDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RentDetails_TaxDeclarationId",
                schema: "pay",
                table: "RentDetails",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationLines_CustomerId_OrgId",
                schema: "pay",
                table: "TaxDeclarationLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationLines_TaxDeclarationId",
                schema: "pay",
                table: "TaxDeclarationLines",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_CustomerId_OrgId",
                schema: "pay",
                table: "TaxDeclarations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_CustomerId_OrgId",
                schema: "pay",
                table: "TaxRules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxSlabs_CustomerId_OrgId",
                schema: "pay",
                table: "TaxSlabs",
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
                name: "PreviousEmployerIncomes",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "RentDetails",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxDeclarationLines",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxRules",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxSlabs",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxDeclarations",
                schema: "pay");
        }

        private static readonly string[] Tables =
        [
            "PreviousEmployerIncomes",
            "RentDetails",
            "TaxDeclarationLines",
            "TaxDeclarations",
            "TaxRules",
            "TaxSlabs"
        ];
    }
}
