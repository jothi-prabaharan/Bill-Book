using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RestaurantBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPriceInclusive",
                schema: "sal",
                table: "RestaurantOrderLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStocked",
                schema: "sal",
                table: "RestaurantOrderLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "RestaurantBillId",
                schema: "sal",
                table: "RestaurantOrderLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TaxGroupId",
                schema: "sal",
                table: "RestaurantOrderLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxTreatment",
                schema: "sal",
                table: "RestaurantOrderLines",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                // Lines ordered before TK-148 carried no tax treatment; they were taxable.
                defaultValue: "Taxable");

            migrationBuilder.AddColumn<long>(
                name: "UomId",
                schema: "sal",
                table: "RestaurantOrderLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "RestaurantBills",
                schema: "sal",
                columns: table => new
                {
                    RestaurantBillId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceChargePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ServiceChargeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TipAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TipBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    BilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_RestaurantBills", x => x.RestaurantBillId);
                    table.CheckConstraint("chk_restaurant_bill_tip", "\"TipAmount\" >= 0 AND (\"TipAmount\" = 0 OR \"TipBankAccountId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RestaurantBills_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_RestaurantBillId",
                schema: "sal",
                table: "RestaurantOrderLines",
                column: "RestaurantBillId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantBills_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantBills",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantBills_InvoiceId",
                schema: "sal",
                table: "RestaurantBills",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantBills_OrgId_BilledAt",
                schema: "sal",
                table: "RestaurantBills",
                columns: new[] { "OrgId", "BilledAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantOrderLines_RestaurantBills_RestaurantBillId",
                schema: "sal",
                table: "RestaurantOrderLines",
                column: "RestaurantBillId",
                principalSchema: "sal",
                principalTable: "RestaurantBills",
                principalColumn: "RestaurantBillId",
                onDelete: ReferentialAction.Restrict);

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "sal", "RestaurantBills");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantOrderLines_RestaurantBills_RestaurantBillId",
                schema: "sal",
                table: "RestaurantOrderLines");

            migrationBuilder.DropTable(
                name: "RestaurantBills",
                schema: "sal");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantOrderLines_RestaurantBillId",
                schema: "sal",
                table: "RestaurantOrderLines");

            migrationBuilder.DropColumn(
                name: "IsPriceInclusive",
                schema: "sal",
                table: "RestaurantOrderLines");

            migrationBuilder.DropColumn(
                name: "IsStocked",
                schema: "sal",
                table: "RestaurantOrderLines");

            migrationBuilder.DropColumn(
                name: "RestaurantBillId",
                schema: "sal",
                table: "RestaurantOrderLines");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                schema: "sal",
                table: "RestaurantOrderLines");

            migrationBuilder.DropColumn(
                name: "TaxTreatment",
                schema: "sal",
                table: "RestaurantOrderLines");

            migrationBuilder.DropColumn(
                name: "UomId",
                schema: "sal",
                table: "RestaurantOrderLines");
        }
    }
}
