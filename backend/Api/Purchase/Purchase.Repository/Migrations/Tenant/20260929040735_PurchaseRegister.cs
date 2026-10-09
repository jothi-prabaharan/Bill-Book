using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PurchaseRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The purchase GST register, reverse charge and ITC eligibility (TK-115, D-30, D-31).
            migrationBuilder.AddColumn<bool>(
                name: "IsReverseCharge",
                schema: "pur",
                table: "Bills",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ItcEligibility",
                schema: "pur",
                table: "BillDetails",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Eligible");

            migrationBuilder.CreateTable(
                name: "PurchaseRegister",
                schema: "pur",
                columns: table => new
                {
                    PurchaseRegisterId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    SourceLineId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplierInvoiceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierInvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    ReverseCharge = table.Column<bool>(type: "boolean", nullable: false),
                    ItcEligibility = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    LineType = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    GstRate = table.Column<decimal>(type: "numeric(7,3)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    TaxableAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    OriginalBillId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalBillNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_PurchaseRegister", x => x.PurchaseRegisterId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRegister_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseRegister",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRegister_Date",
                schema: "pur",
                table: "PurchaseRegister",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRegister_Source",
                schema: "pur",
                table: "PurchaseRegister",
                columns: new[] { "OrgId", "TransactionTypeCode", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRegister_Supplier",
                schema: "pur",
                table: "PurchaseRegister",
                columns: new[] { "OrgId", "ContactGstin", "SupplierInvoiceNo" });

            // TK-71's policy, as every pur table carrying CustomerId and OrgId has it.
            migrationBuilder.Sql("""
                ALTER TABLE pur."PurchaseRegister" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pur."PurchaseRegister" FORCE ROW LEVEL SECURITY;
                CREATE POLICY purchaseregister_tenant_isolation
                    ON pur."PurchaseRegister"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseRegister",
                schema: "pur");

            migrationBuilder.DropColumn(
                name: "IsReverseCharge",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ItcEligibility",
                schema: "pur",
                table: "BillDetails");
        }
    }
}
