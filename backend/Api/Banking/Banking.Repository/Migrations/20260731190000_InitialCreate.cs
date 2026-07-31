using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Banking.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bnk");
            migrationBuilder.CreateTable(
                name: "Banks",
                schema: "bnk",
                columns: table => new
                {
                    BankId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.BankId);
                });
            migrationBuilder.CreateTable(
                name: "BankAccounts",
                schema: "bnk",
                columns: table => new
                {
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankId = table.Column<long>(type: "bigint", nullable: true),
                    LedgerAccountId = table.Column<long>(type: "bigint", nullable: true),
                    AccountName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Ifsc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Micr = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    SwiftCode = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OdLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.BankAccountId);
                    table.CheckConstraint("chk_bank_account_institution", "\"AccountType\" IN ('Cash', 'Wallet') OR \"BankId\" IS NOT NULL");
                    table.CheckConstraint("chk_bank_account_od_limit", "\"OdLimit\" IS NULL OR \"AccountType\" IN ('OverDraft', 'CashCredit', 'CreditCard')");
                    table.ForeignKey(
                        name: "FK_BankAccounts_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "bnk",
                        principalTable: "Banks",
                        principalColumn: "BankId",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(
                name: "IX_Banks_OrgId",
                schema: "bnk",
                table: "Banks",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_Banks_OrgId_BankCode",
                schema: "bnk",
                table: "Banks",
                columns: new[] { "OrgId", "BankCode" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Banks_Order",
                schema: "bnk",
                table: "Banks",
                columns: new[] { "OrgId", "DisplayOrder", "BankName" });
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_OrgId",
                schema: "bnk",
                table: "BankAccounts",
                column: "OrgId");
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BankId",
                schema: "bnk",
                table: "BankAccounts",
                column: "BankId");
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_OrgId_BankId_AccountNumber",
                schema: "bnk",
                table: "BankAccounts",
                columns: new[] { "OrgId", "BankId", "AccountNumber" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Ledger",
                schema: "bnk",
                table: "BankAccounts",
                columns: new[] { "OrgId", "LedgerAccountId" },
                unique: true,
                filter: "\"LedgerAccountId\" IS NOT NULL");
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Default",
                schema: "bnk",
                table: "BankAccounts",
                column: "OrgId",
                unique: true,
                filter: "\"IsDefault\" = true");
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Order",
                schema: "bnk",
                table: "BankAccounts",
                columns: new[] { "OrgId", "DisplayOrder", "AccountName" });
            // Row-level security on both bnk tables.
            foreach (string table in new[] { "Banks", "BankAccounts" })
            {
                migrationBuilder.Sql($"ALTER TABLE bnk.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON bnk.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[] { "BankAccounts", "Banks" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON bnk.\"{table}\";");
            }

            migrationBuilder.DropTable(name: "BankAccounts", schema: "bnk");
            migrationBuilder.DropTable(name: "Banks", schema: "bnk");
        }
    }
}
