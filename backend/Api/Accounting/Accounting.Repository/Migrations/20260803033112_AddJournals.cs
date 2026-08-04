using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddJournals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Journals",
                schema: "acc",
                columns: table => new
                {
                    JournalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JournalNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    JournalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversesJournalId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByJournalId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.JournalId);
                    table.CheckConstraint("chk_journal_number_on_post", "(\"Status\" = 'Draft' AND \"JournalNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"JournalNo\" IS NOT NULL)");
                    table.CheckConstraint("chk_journal_posted_stamp", "(\"Status\" = 'Draft') = (\"PostedAt\" IS NULL)");
                    table.CheckConstraint("chk_journal_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_journal_reversal_distinct", "\"ReversesJournalId\" IS NULL OR \"ReversesJournalId\" <> \"JournalId\"");
                    table.ForeignKey(
                        name: "FK_Journals_Journals_ReversedByJournalId",
                        column: x => x.ReversedByJournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Journals_Journals_ReversesJournalId",
                        column: x => x.ReversesJournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalDetails",
                schema: "acc",
                columns: table => new
                {
                    JournalDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JournalId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: true),
                    DebitAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DebitAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmountBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineMemo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReversesJournalDetailId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByJournalDetailId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalDetails", x => x.JournalDetailId);
                    table.CheckConstraint("chk_journal_detail_exclusive", "(\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");
                    table.CheckConstraint("chk_journal_detail_non_negative", "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0 AND \"DebitAmountBase\" >= 0 AND \"CreditAmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_JournalDetails_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalDetails_JournalDetails_ReversedByJournalDetailId",
                        column: x => x.ReversedByJournalDetailId,
                        principalSchema: "acc",
                        principalTable: "JournalDetails",
                        principalColumn: "JournalDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalDetails_JournalDetails_ReversesJournalDetailId",
                        column: x => x.ReversesJournalDetailId,
                        principalSchema: "acc",
                        principalTable: "JournalDetails",
                        principalColumn: "JournalDetailId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalDetails_Journals_JournalId",
                        column: x => x.JournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalDetails_SubAccounts_SubAccountId",
                        column: x => x.SubAccountId,
                        principalSchema: "acc",
                        principalTable: "SubAccounts",
                        principalColumn: "SubAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_AccountId",
                schema: "acc",
                table: "JournalDetails",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_JournalId_LineNumber",
                schema: "acc",
                table: "JournalDetails",
                columns: new[] { "JournalId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_OrgId",
                schema: "acc",
                table: "JournalDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_ReversedByJournalDetailId",
                schema: "acc",
                table: "JournalDetails",
                column: "ReversedByJournalDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_ReversesJournalDetailId",
                schema: "acc",
                table: "JournalDetails",
                column: "ReversesJournalDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_SubAccountId",
                schema: "acc",
                table: "JournalDetails",
                column: "SubAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_Number",
                schema: "acc",
                table: "Journals",
                columns: new[] { "OrgId", "JournalNo" },
                unique: true,
                filter: "\"JournalNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_OrgId",
                schema: "acc",
                table: "Journals",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_OrgId_JournalDate",
                schema: "acc",
                table: "Journals",
                columns: new[] { "OrgId", "JournalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Journals_OrgId_TransactionTypeCode_SourceId",
                schema: "acc",
                table: "Journals",
                columns: new[] { "OrgId", "TransactionTypeCode", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Journals_ReversedByJournalId",
                schema: "acc",
                table: "Journals",
                column: "ReversedByJournalId");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_ReversesJournalId",
                schema: "acc",
                table: "Journals",
                column: "ReversesJournalId");

            // The balance check. Not a check constraint: it spans rows, so it
            // has to be a trigger.
            //
            // Two things about it, and both are load-bearing.
            //
            // DEFERRABLE INITIALLY DEFERRED, because a journal is several lines
            // and is only balanced once all of them are in. An immediate trigger
            // would reject every multi-line entry on its first row.
            //
            // **Posted entries only.** A draft is allowed to be unbalanced —
            // that is what a draft is for. Someone keying a twelve-line accrual
            // is out of balance for eleven of them, and a database that refused
            // to save that would force the whole entry to be typed in one
            // sitting or not at all.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION acc.assert_journal_balanced() RETURNS trigger AS $$
                DECLARE
                    journal bigint;
                    state varchar(10);
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        journal := OLD."JournalId";
                    ELSE
                        journal := NEW."JournalId";
                    END IF;

                    SELECT "Status" INTO state FROM acc."Journals" WHERE "JournalId" = journal;

                    -- The header is gone: the whole journal was deleted and its
                    -- lines cascaded. There is nothing left to be unbalanced.
                    IF state IS NULL OR state = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM("DebitAmountBase"), 0), COALESCE(SUM("CreditAmountBase"), 0)
                      INTO debits, credits
                      FROM acc."JournalDetails"
                     WHERE "JournalId" = journal;

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'Journal % does not balance: debits %, credits %',
                            journal, debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_journal_balanced ON acc.\"JournalDetails\";");

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_journal_balanced
                AFTER INSERT OR UPDATE OR DELETE ON acc."JournalDetails"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_journal_balanced();
                """);

            // Posting is a header update, and the lines do not move — so the
            // line trigger above never fires for it. Without this second one, a
            // draft that does not balance could be posted simply by flipping its
            // status, which is the one path that matters most.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION acc.assert_journal_balanced_on_post() RETURNS trigger AS $$
                DECLARE
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF NEW."Status" = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM("DebitAmountBase"), 0), COALESCE(SUM("CreditAmountBase"), 0)
                      INTO debits, credits
                      FROM acc."JournalDetails"
                     WHERE "JournalId" = NEW."JournalId";

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'Journal % does not balance: debits %, credits %',
                            NEW."JournalId", debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_journal_balanced_on_post ON acc.\"Journals\";");

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_journal_balanced_on_post
                AFTER INSERT OR UPDATE ON acc."Journals"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_journal_balanced_on_post();
                """);

            // Row-level security, as on every other per-customer table. The EF
            // query filter is the first line of defence, not the last: it is a
            // property of the code, and one query written with
            // IgnoreQueryFilters would read another branch's journals.
            foreach (string table in new[] { "Journals", "JournalDetails" })
            {
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON acc.\"{table}\";");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON acc.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS journaldetails_org_isolation ON acc.\"JournalDetails\";");
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS journals_org_isolation ON acc.\"Journals\";");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_journal_balanced_on_post ON acc.\"Journals\";");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_journal_balanced ON acc.\"JournalDetails\";");

            migrationBuilder.DropTable(
                name: "JournalDetails",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Journals",
                schema: "acc");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS acc.assert_journal_balanced_on_post();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS acc.assert_journal_balanced();");
        }
    }
}
