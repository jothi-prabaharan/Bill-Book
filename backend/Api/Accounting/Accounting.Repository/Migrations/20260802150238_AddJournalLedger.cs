using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalLedger",
                schema: "acc",
                columns: table => new
                {
                    LedgerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LedgerDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TransactionId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionDetailId = table.Column<long>(type: "bigint", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DebitAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    TaxExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    LedgerTypeId = table.Column<int>(type: "integer", nullable: false),
                    LedgerSourceId = table.Column<int>(type: "integer", nullable: false),
                    SourceDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionDesc = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MappingTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    MappingTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    JournalId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalLedger", x => x.LedgerId);
                    table.CheckConstraint("chk_ledger_base_exclusive", "\"DebitAmountBase\" = 0 OR \"CreditAmountBase\" = 0");
                    table.CheckConstraint("chk_ledger_exclusive", "(\"DebitAmount\" = 0) <> (\"CreditAmount\" = 0)");
                    table.CheckConstraint("chk_ledger_non_negative", "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0 AND \"DebitAmountBase\" >= 0 AND \"CreditAmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_JournalLedger_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalLedger_SubAccounts_SubAccountId",
                        column: x => x.SubAccountId,
                        principalSchema: "acc",
                        principalTable: "SubAccounts",
                        principalColumn: "SubAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_AccountId",
                schema: "acc",
                table: "JournalLedger",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_Mapping",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId",
                schema: "acc",
                table: "JournalLedger",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_AccountId_LedgerDate",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "AccountId", "LedgerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_ContactId",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_LedgerDate",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "LedgerDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_SubAccountId",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "SubAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_Posting",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "TransactionTypeCode", "TransactionId", "TransactionDetailId", "LedgerTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_SubAccountId",
                schema: "acc",
                table: "JournalLedger",
                column: "SubAccountId");

            // The balance check. Not expressible as a check constraint: it spans
            // rows, so it has to be a trigger.
            //
            // DEFERRABLE INITIALLY DEFERRED is the whole point. A posting is
            // several rows and is only balanced once all of them are in, so an
            // immediate trigger would reject every multi-leg posting on its
            // first row. Deferred, it fires once at commit and judges the set.
            //
            // It runs as the invoker, so the SELECT below is subject to the RLS
            // policy added underneath — which is correct here, because the rows
            // being summed are always the current organization's.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION acc.assert_ledger_balanced() RETURNS trigger AS $$
                DECLARE
                    org uuid;
                    code varchar(3);
                    txn bigint;
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        org := OLD."OrgId";
                        code := OLD."TransactionTypeCode";
                        txn := OLD."TransactionId";
                    ELSE
                        org := NEW."OrgId";
                        code := NEW."TransactionTypeCode";
                        txn := NEW."TransactionId";
                    END IF;

                    SELECT COALESCE(SUM("DebitAmountBase"), 0), COALESCE(SUM("CreditAmountBase"), 0)
                      INTO debits, credits
                      FROM acc."JournalLedger"
                     WHERE "OrgId" = org
                       AND "TransactionTypeCode" = code
                       AND "TransactionId" = txn;

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'Ledger postings for %-% do not balance: debits %, credits %',
                            code, txn, debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_ledger_balanced ON acc.\"JournalLedger\";");

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_ledger_balanced
                AFTER INSERT OR UPDATE OR DELETE ON acc."JournalLedger"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_ledger_balanced();
                """);

            // Row-level security, as on every other per-customer table. The EF
            // query filter is the first line of defence, not the last: it is a
            // property of the code, and one query written with
            // IgnoreQueryFilters would read another branch's general ledger.
            migrationBuilder.Sql("ALTER TABLE acc.\"JournalLedger\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS journal_ledger_org_isolation ON acc.\"JournalLedger\";");
            migrationBuilder.Sql(
                "CREATE POLICY journal_ledger_org_isolation ON acc.\"JournalLedger\" " +
                "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS journal_ledger_org_isolation ON acc.\"JournalLedger\";");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_ledger_balanced ON acc.\"JournalLedger\";");

            migrationBuilder.DropTable(
                name: "JournalLedger",
                schema: "acc");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS acc.assert_ledger_balanced();");
        }
    }
}
