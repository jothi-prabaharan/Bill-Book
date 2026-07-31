using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                schema: "acc",
                columns: table => new
                {
                    PaymentTermId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermSystemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TermName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TermType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDays = table.Column<int>(type: "integer", nullable: false),
                    DueDayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    DiscountDays = table.Column<int>(type: "integer", nullable: false),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.PaymentTermId);
                    table.CheckConstraint("chk_term_applicability", "\"IsSales\" = true OR \"IsPurchase\" = true");
                    table.CheckConstraint("chk_term_day_of_month", "(\"TermType\" = 'DayOfNextMonth' AND \"DueDayOfMonth\" IS NOT NULL) OR (\"TermType\" <> 'DayOfNextMonth' AND \"DueDayOfMonth\" IS NULL)");
                    table.CheckConstraint("chk_term_discount_days", "\"DiscountPercent\" = 0 OR \"DiscountDays\" > 0");
                    table.CheckConstraint("chk_term_discount_window", "\"TermType\" <> 'Net' OR \"DiscountDays\" <= \"DueDays\"");
                    table.CheckConstraint("chk_term_due_on_receipt", "\"TermType\" <> 'DueOnReceipt' OR \"DueDays\" = 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_OrgId",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Default",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Sales",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId",
                filter: "\"IsSales\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Purchase",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_OrgId_TermName",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "OrgId", "TermName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_SystemName",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "OrgId", "TermSystemName" },
                unique: true,
                filter: "\"TermSystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_Order",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "OrgId", "DisplayOrder", "TermName" });

            migrationBuilder.Sql(
                "ALTER TABLE acc.\"PaymentTerms\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "CREATE POLICY payment_terms_org_isolation ON acc.\"PaymentTerms\" " +
                "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS payment_terms_org_isolation ON acc.\"PaymentTerms\";");

            migrationBuilder.DropTable(
                name: "PaymentTerms",
                schema: "acc");
        }
    }
}
