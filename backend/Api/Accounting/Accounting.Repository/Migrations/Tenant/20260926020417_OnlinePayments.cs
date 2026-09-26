using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class OnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnlinePaymentAccount",
                schema: "acc",
                table: "BankAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OnlinePayments",
                schema: "acc",
                columns: table => new
                {
                    OnlinePaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Allocations = table.Column<string>(type: "jsonb", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Gateway = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GatewayOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GatewayPaymentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ReceiveMoneyId = table.Column<long>(type: "bigint", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_OnlinePayments", x => x.OnlinePaymentId);
                    table.CheckConstraint("chk_onlinepayments_paid_stamp", "(\"Status\" IN ('Paid', 'Refunded')) = (\"PaidAt\" IS NOT NULL)");
                    table.CheckConstraint("chk_onlinepayments_receipt_paid", "\"ReceiveMoneyId\" IS NULL OR \"Status\" IN ('Paid', 'Refunded')");
                    table.ForeignKey(
                        name: "FK_OnlinePayments_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "acc",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnlinePayments_ReceiveMoney_ReceiveMoneyId",
                        column: x => x.ReceiveMoneyId,
                        principalSchema: "acc",
                        principalTable: "ReceiveMoney",
                        principalColumn: "ReceiveMoneyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_BankAccountId",
                schema: "acc",
                table: "OnlinePayments",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_CustomerId_OrgId",
                schema: "acc",
                table: "OnlinePayments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_Gateway_GatewayOrderId",
                schema: "acc",
                table: "OnlinePayments",
                columns: new[] { "Gateway", "GatewayOrderId" },
                unique: true,
                filter: "\"GatewayOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_Gateway_GatewayPaymentId",
                schema: "acc",
                table: "OnlinePayments",
                columns: new[] { "Gateway", "GatewayPaymentId" },
                unique: true,
                filter: "\"GatewayPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_OrgId_ContactId",
                schema: "acc",
                table: "OnlinePayments",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_ReceiveMoneyId",
                schema: "acc",
                table: "OnlinePayments",
                column: "ReceiveMoneyId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_Reference",
                schema: "acc",
                table: "OnlinePayments",
                column: "Reference",
                unique: true);

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF. The gateway callback sets both from the
            // payment's reference before it opens the database (TK-98).
            migrationBuilder.Sql("""
                ALTER TABLE acc."OnlinePayments" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acc."OnlinePayments" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS onlinepayments_tenant_isolation ON acc."OnlinePayments";
                CREATE POLICY onlinepayments_tenant_isolation
                    ON acc."OnlinePayments"
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
                name: "OnlinePayments",
                schema: "acc");

            migrationBuilder.DropColumn(
                name: "IsOnlinePaymentAccount",
                schema: "acc",
                table: "BankAccounts");
        }
    }
}
