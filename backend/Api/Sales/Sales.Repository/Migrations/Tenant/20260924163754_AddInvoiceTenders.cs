using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddInvoiceTenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceTenders",
                schema: "sal",
                columns: table => new
                {
                    InvoiceTenderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_InvoiceTenders", x => x.InvoiceTenderId);
                    table.CheckConstraint("chk_invoicetender_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceTenders_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTenders_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceTenders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTenders_InvoiceId",
                schema: "sal",
                table: "InvoiceTenders",
                column: "InvoiceId");

            // Row-level security for the new table, the TK-71 block as in
            // 20260924061419_EnableRowLevelSecurity.cs (TK-39).
            migrationBuilder.Sql("""
                ALTER TABLE sal."InvoiceTenders" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sal."InvoiceTenders" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS invoicetenders_tenant_isolation ON sal."InvoiceTenders";
                CREATE POLICY invoicetenders_tenant_isolation
                    ON sal."InvoiceTenders"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP POLICY IF EXISTS invoicetenders_tenant_isolation ON sal."InvoiceTenders";""");

            migrationBuilder.DropTable(
                name: "InvoiceTenders",
                schema: "sal");
        }
    }
}
