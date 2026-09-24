using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RestoreLedgerTriggers : Migration
    {
        /// <summary>The money documents whose detail lines must add up to the header once posted.</summary>
        private static readonly (string Parent, string Child, string Key)[] MoneyDocuments =
        [
            ("SpendMoney", "SpendMoneyDetails", "SpendMoneyId"),
            ("ReceiveMoney", "ReceiveMoneyDetails", "ReceiveMoneyId"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The database's half of the ledger's balance rule, written by hand
            // because EF Core generates no functions or triggers. The chain
            // squashed on 14 September 2026 (2c5ed6f) dropped every block that
            // used to do this, and TK-07 in docs/TASKS.md restores them from
            // 2c5ed6f^:…/20260902151402_InitialAccountingSchema.cs, checked
            // against today's columns (all of them still exist).
            //
            // Three checks guard a posting: LedgerPostingService refuses an
            // unbalanced request, JournalService refuses to post an unbalanced
            // journal, and these refuse to commit either. Each trigger is a
            // CONSTRAINT TRIGGER, DEFERRABLE INITIALLY DEFERRED, so it fires at
            // commit: a multi-line insert would otherwise trip on the first row.
            //
            // They run as the invoker, so row-level security applies to the
            // sums. That is correct rather than a gap: a row can only be written
            // under its own tenant (the policy's USING doubles as its check), and
            // every sum below is narrowed to that row's branch.
            //
            // Two changes from the pre-squash text, both in the ledger trigger:
            //   - A DELETE read NEW."OrgId", which is NULL on a delete, so the sum
            //     ran over no rows and passed. Removing one leg of a posting was
            //     never checked. It reads OLD on a delete now.
            //   - The branch-wide sum ran once per changed row, so a posting of n
            //     legs summed the whole branch ledger n times at commit, and an
            //     opening balance of thousands of lines grew quadratically. Every
            //     deferred event fires after the last statement, against the same
            //     final state, so one check per branch per transaction is enough;
            //     a transaction-local marker records it. (SET CONSTRAINTS …
            //     IMMEDIATE would fire events mid-transaction and defeat that; no
            //     code issues it.)

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acc.assert_ledger_balanced() RETURNS trigger AS $$
                DECLARE
                    org uuid;
                    marker text;
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        org := OLD.""OrgId"";
                    ELSE
                        org := NEW.""OrgId"";
                    END IF;

                    marker := txid_current()::text || ':' || org::text;

                    IF current_setting('acc.ledger_checked', true) = marker THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM(""DebitAmountBase""), 0), COALESCE(SUM(""CreditAmountBase""), 0)
                      INTO debits, credits
                      FROM acc.""JournalLedger""
                     WHERE ""OrgId"" = org;

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'The branch ledger does not balance: debits %, credits %',
                            debits, credits;
                    END IF;

                    PERFORM set_config('acc.ledger_checked', marker, true);

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                ");

            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_ledger_balanced ON acc.""JournalLedger"";");

            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_ledger_balanced
                AFTER INSERT OR UPDATE OR DELETE ON acc.""JournalLedger""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_ledger_balanced();
                ");

            // A journal's lines, once it has left Draft. Two triggers, because
            // posting a draft changes only the header — the lines are untouched —
            // so a trigger on the lines alone would never see the post.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acc.assert_journal_balanced() RETURNS trigger AS $$
                DECLARE
                    journal bigint;
                    state varchar(10);
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        journal := OLD.""JournalId"";
                    ELSE
                        journal := NEW.""JournalId"";
                    END IF;

                    SELECT ""Status"" INTO state FROM acc.""Journals"" WHERE ""JournalId"" = journal;

                    IF state IS NULL OR state = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM(""DebitAmountBase""), 0), COALESCE(SUM(""CreditAmountBase""), 0)
                      INTO debits, credits
                      FROM acc.""JournalDetails""
                     WHERE ""JournalId"" = journal;

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'Journal % does not balance: debits %, credits %',
                            journal, debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                ");

            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_journal_balanced ON acc.""JournalDetails"";");

            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_journal_balanced
                AFTER INSERT OR UPDATE OR DELETE ON acc.""JournalDetails""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_journal_balanced();
                ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acc.assert_journal_balanced_on_post() RETURNS trigger AS $$
                DECLARE
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    IF NEW.""Status"" = 'Draft' THEN
                        RETURN NULL;
                    END IF;

                    SELECT COALESCE(SUM(""DebitAmountBase""), 0), COALESCE(SUM(""CreditAmountBase""), 0)
                      INTO debits, credits
                      FROM acc.""JournalDetails""
                     WHERE ""JournalId"" = NEW.""JournalId"";

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'Journal % does not balance: debits %, credits %',
                            NEW.""JournalId"", debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                ");

            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_journal_balanced_on_post ON acc.""Journals"";");

            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_journal_balanced_on_post
                AFTER INSERT OR UPDATE ON acc.""Journals""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_journal_balanced_on_post();
                ");

            // A posted money document's lines add up to its amount. A draft's need
            // not; a voided one keeps its lines and its amount, so still agrees.
            // The same pair of triggers as the journal, for the same reason.
            foreach ((string parent, string child, string key) in MoneyDocuments)
            {
                string fn = parent.ToLowerInvariant();

                migrationBuilder.Sql($@"
                    CREATE OR REPLACE FUNCTION acc.assert_{fn}_allocated() RETURNS trigger AS $$
                    DECLARE
                        doc bigint;
                        state varchar(10);
                        header numeric(18,2);
                        allocated numeric(18,2);
                    BEGIN
                        IF TG_OP = 'DELETE' THEN
                            doc := OLD.""{key}"";
                        ELSE
                            doc := NEW.""{key}"";
                        END IF;

                        SELECT ""Status"", ""Amount"" INTO state, header
                          FROM acc.""{parent}"" WHERE ""{key}"" = doc;

                        IF state IS NULL OR state = 'Draft' THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM(""Amount""), 0) INTO allocated
                          FROM acc.""{child}"" WHERE ""{key}"" = doc;

                        IF allocated <> header THEN
                            RAISE EXCEPTION
                                '{parent} % is allocated %, but its amount is %',
                                doc, allocated, header;
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");

                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{fn}_allocated ON acc.\"{child}\";");

                migrationBuilder.Sql($@"
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated
                    AFTER INSERT OR UPDATE OR DELETE ON acc.""{child}""
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION acc.assert_{fn}_allocated();
                    ");

                migrationBuilder.Sql($@"
                    CREATE OR REPLACE FUNCTION acc.assert_{fn}_allocated_on_post() RETURNS trigger AS $$
                    DECLARE
                        allocated numeric(18,2);
                    BEGIN
                        IF NEW.""Status"" = 'Draft' THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM(""Amount""), 0) INTO allocated
                          FROM acc.""{child}"" WHERE ""{key}"" = NEW.""{key}"";

                        IF allocated <> NEW.""Amount"" THEN
                            RAISE EXCEPTION
                                '{parent} % is allocated %, but its amount is %',
                                NEW.""{key}"", allocated, NEW.""Amount"";
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");

                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{fn}_allocated_on_post ON acc.\"{parent}\";");

                migrationBuilder.Sql($@"
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated_on_post
                    AFTER INSERT OR UPDATE ON acc.""{parent}""
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION acc.assert_{fn}_allocated_on_post();
                    ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach ((string parent, string child, _) in MoneyDocuments)
            {
                string fn = parent.ToLowerInvariant();

                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{fn}_allocated_on_post ON acc.\"{parent}\";");
                migrationBuilder.Sql($"DROP FUNCTION IF EXISTS acc.assert_{fn}_allocated_on_post();");
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{fn}_allocated ON acc.\"{child}\";");
                migrationBuilder.Sql($"DROP FUNCTION IF EXISTS acc.assert_{fn}_allocated();");
            }

            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_journal_balanced_on_post ON acc.""Journals"";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS acc.assert_journal_balanced_on_post();");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_journal_balanced ON acc.""JournalDetails"";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS acc.assert_journal_balanced();");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_ledger_balanced ON acc.""JournalLedger"";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS acc.assert_ledger_balanced();");
        }
    }
}
