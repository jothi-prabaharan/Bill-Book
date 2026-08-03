using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Banking.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoneyTransactions",
                schema: "bnk",
                columns: table => new
                {
                    MoneyTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TransactionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ToBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyTransactions", x => x.MoneyTransactionId);
                    table.CheckConstraint("chk_money_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("chk_money_number_on_post", "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");
                    table.CheckConstraint("chk_money_posted_stamp", "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");
                    table.CheckConstraint("chk_money_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_money_transaction_type", "\"TransactionTypeCode\" IN ('SPM', 'RCM', 'TRM')");
                    table.CheckConstraint("chk_money_transfer_distinct", "\"ToBankAccountId\" IS NULL OR \"ToBankAccountId\" <> \"BankAccountId\"");
                    table.CheckConstraint("chk_money_transfer_shape", "(\"TransactionTypeCode\" = 'TRM' AND \"ToBankAccountId\" IS NOT NULL AND \"ContactId\" IS NULL) OR (\"TransactionTypeCode\" <> 'TRM' AND \"ToBankAccountId\" IS NULL)");
                    table.CheckConstraint("chk_money_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_MoneyTransactions_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MoneyTransactions_BankAccounts_ToBankAccountId",
                        column: x => x.ToBankAccountId,
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MoneyTransactionDetails",
                schema: "bnk",
                columns: table => new
                {
                    MoneyTransactionDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MoneyTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    LedgerSourceId = table.Column<int>(type: "integer", nullable: false),
                    MappingTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MappingTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineMemo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyTransactionDetails", x => x.MoneyTransactionDetailId);
                    table.CheckConstraint("chk_money_detail_amount_positive", "\"Amount\" > 0 AND \"AmountBase\" > 0");
                    table.CheckConstraint("chk_money_detail_mapping_paired", "(\"MappingTransactionTypeCode\" IS NULL) = (\"MappingTransactionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_MoneyTransactionDetails_MoneyTransactions_MoneyTransactionId",
                        column: x => x.MoneyTransactionId,
                        principalSchema: "bnk",
                        principalTable: "MoneyTransactions",
                        principalColumn: "MoneyTransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactionDetails_Mapping",
                schema: "bnk",
                table: "MoneyTransactionDetails",
                columns: new[] { "OrgId", "MappingTransactionTypeCode", "MappingTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactionDetails_MoneyTransactionId_LineNumber",
                schema: "bnk",
                table: "MoneyTransactionDetails",
                columns: new[] { "MoneyTransactionId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactionDetails_OrgId",
                schema: "bnk",
                table: "MoneyTransactionDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactions_Account",
                schema: "bnk",
                table: "MoneyTransactions",
                columns: new[] { "OrgId", "BankAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactions_BankAccountId",
                schema: "bnk",
                table: "MoneyTransactions",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactions_Number",
                schema: "bnk",
                table: "MoneyTransactions",
                columns: new[] { "OrgId", "TransactionTypeCode", "TransactionNo" },
                unique: true,
                filter: "\"TransactionNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactions_OrgId",
                schema: "bnk",
                table: "MoneyTransactions",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactions_OrgId_ContactId",
                schema: "bnk",
                table: "MoneyTransactions",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactions_OrgId_TransactionDate",
                schema: "bnk",
                table: "MoneyTransactions",
                columns: new[] { "OrgId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyTransactions_ToBankAccountId",
                schema: "bnk",
                table: "MoneyTransactions",
                column: "ToBankAccountId");

            // The lines have to add up to the header. Not a check constraint:
            // it spans rows, so it has to be a trigger.
            //
            // Two things about it, and both are load-bearing.
            //
            // DEFERRABLE INITIALLY DEFERRED, because a document is several lines
            // and only adds up once all of them are in. An immediate trigger
            // would reject every multi-line payment on its first row.
            //
            // **Posted documents only.** A draft is allowed not to add up — that
            // is what a draft is for. Someone allocating a payment across nine
            // bills is short for eight of them, and a database that refused to
            // save that would force the whole allocation to be keyed in one
            // sitting or not at all.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION bnk.assert_money_allocated() RETURNS trigger AS $$
                DECLARE
                    txn bigint;
                    state varchar(10);
                    header numeric(18,2);
                    lines numeric(18,2);
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        txn := OLD."MoneyTransactionId";
                    ELSE
                        txn := NEW."MoneyTransactionId";
                    END IF;

                    SELECT "Status", "Amount" INTO state, header
                      FROM bnk."MoneyTransactions" WHERE "MoneyTransactionId" = txn;

                    -- The header is gone: the whole document was deleted and its
                    -- lines cascaded. There is nothing left to reconcile.
                    IF state IS NULL OR state = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM("Amount"), 0) INTO lines
                      FROM bnk."MoneyTransactionDetails" WHERE "MoneyTransactionId" = txn;

                    IF lines <> header THEN
                        RAISE EXCEPTION
                            'Money transaction % is allocated %, but its amount is %',
                            txn, lines, header;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_money_allocated ON bnk.\"MoneyTransactionDetails\";");

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_money_allocated
                AFTER INSERT OR UPDATE OR DELETE ON bnk."MoneyTransactionDetails"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION bnk.assert_money_allocated();
                """);

            // Posting is a header update and the lines do not move, so the line
            // trigger above never fires for it. Without this second one, a draft
            // whose allocation is short could be posted simply by flipping its
            // status — which is the one path that matters most. The same pair the
            // manual journal carries, for the same reason.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION bnk.assert_money_allocated_on_post() RETURNS trigger AS $$
                DECLARE
                    lines numeric(18,2);
                BEGIN
                    IF NEW."Status" = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM("Amount"), 0) INTO lines
                      FROM bnk."MoneyTransactionDetails"
                     WHERE "MoneyTransactionId" = NEW."MoneyTransactionId";

                    IF lines <> NEW."Amount" THEN
                        RAISE EXCEPTION
                            'Money transaction % is allocated %, but its amount is %',
                            NEW."MoneyTransactionId", lines, NEW."Amount";
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_money_allocated_on_post ON bnk.\"MoneyTransactions\";");

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_money_allocated_on_post
                AFTER INSERT OR UPDATE ON bnk."MoneyTransactions"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION bnk.assert_money_allocated_on_post();
                """);

            // Row-level security, as on every other per-customer table. The EF
            // query filter is the first line of defence, not the last: it is a
            // property of the code, and one query written with IgnoreQueryFilters
            // would read another branch's payments.
            foreach (string table in new[] { "MoneyTransactions", "MoneyTransactionDetails" })
            {
                migrationBuilder.Sql($"ALTER TABLE bnk.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON bnk.\"{table}\";");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON bnk.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS moneytransactiondetails_org_isolation "
                    + "ON bnk.\"MoneyTransactionDetails\";");
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS moneytransactions_org_isolation "
                    + "ON bnk.\"MoneyTransactions\";");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_money_allocated_on_post ON bnk.\"MoneyTransactions\";");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_money_allocated ON bnk.\"MoneyTransactionDetails\";");

            migrationBuilder.DropTable(
                name: "MoneyTransactionDetails",
                schema: "bnk");

            migrationBuilder.DropTable(
                name: "MoneyTransactions",
                schema: "bnk");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS bnk.assert_money_allocated_on_post();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS bnk.assert_money_allocated();");
        }
    }
}
