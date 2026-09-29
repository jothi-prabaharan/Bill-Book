using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Gstr2bReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GSTR-2B reconciliation (TK-116).
            migrationBuilder.CreateTable(
                name: "Gstr2bImports",
                schema: "pur",
                columns: table => new
                {
                    Gstr2bImportId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReturnPeriod = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    RecipientGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    MatchedCount = table.Column<int>(type: "integer", nullable: false),
                    MismatchedCount = table.Column<int>(type: "integer", nullable: false),
                    MissingInBooksCount = table.Column<int>(type: "integer", nullable: false),
                    MissingIn2bCount = table.Column<int>(type: "integer", nullable: false),
                    ItcAtRisk = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
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
                    table.PrimaryKey("PK_Gstr2bImports", x => x.Gstr2bImportId);
                });

            migrationBuilder.CreateTable(
                name: "Gstr2bLines",
                schema: "pur",
                columns: table => new
                {
                    Gstr2bLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Gstr2bImportId = table.Column<long>(type: "bigint", nullable: false),
                    MatchState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SupplierGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReverseCharge = table.Column<bool>(type: "boolean", nullable: false),
                    ItcAvailable = table.Column<bool>(type: "boolean", nullable: true),
                    TwoBTaxable = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    TwoBTax = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    BooksTaxable = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    BooksTax = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    BillId = table.Column<long>(type: "bigint", nullable: true),
                    BillNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_Gstr2bLines", x => x.Gstr2bLineId);
                    table.ForeignKey(
                        name: "FK_Gstr2bLines_Gstr2bImports_Gstr2bImportId",
                        column: x => x.Gstr2bImportId,
                        principalSchema: "pur",
                        principalTable: "Gstr2bImports",
                        principalColumn: "Gstr2bImportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gstr2bImports_CustomerId_OrgId",
                schema: "pur",
                table: "Gstr2bImports",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Gstr2bImports_Period",
                schema: "pur",
                table: "Gstr2bImports",
                columns: new[] { "OrgId", "ReturnPeriod" });

            migrationBuilder.CreateIndex(
                name: "IX_Gstr2bLines_CustomerId_OrgId",
                schema: "pur",
                table: "Gstr2bLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Gstr2bLines_Gstr2bImportId",
                schema: "pur",
                table: "Gstr2bLines",
                column: "Gstr2bImportId");

            migrationBuilder.CreateIndex(
                name: "IX_Gstr2bLines_State",
                schema: "pur",
                table: "Gstr2bLines",
                columns: new[] { "OrgId", "Gstr2bImportId", "MatchState" });

            // TK-71's policy, as every pur table carrying CustomerId and OrgId has it.
            foreach (string table in new[] { "Gstr2bImports", "Gstr2bLines" })
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";
                migrationBuilder.Sql($"""
                    ALTER TABLE pur."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE pur."{table}" FORCE ROW LEVEL SECURITY;
                    CREATE POLICY {policy}
                        ON pur."{table}"
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
            migrationBuilder.DropTable(
                name: "Gstr2bLines",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "Gstr2bImports",
                schema: "pur");
        }
    }
}
