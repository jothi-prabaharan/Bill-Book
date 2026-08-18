using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    public partial class RestoreAccountingRls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Accounts",
                "SubAccounts",
                "TaxMasters",
                "PaymentTerms",
                "JournalLedger",
                "Journals",
                "JournalDetails",
                "PeriodLocks",
                "OpeningBalances",
                "OpeningBalanceLines",
                "Banks",
                "BankAccounts",
                "SpendMoney",
                "ReceiveMoney",
                "SpendMoneyDetails",
                "ReceiveMoneyDetails",
                "TransferMoney",
                "BankStatements",
                "BankStatementLines",
                "StatementImportProfiles",
                "TransactionRatios"
            })
            {
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON acc.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON acc.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acc.assert_ledger_balanced() RETURNS trigger AS $$
                DECLARE
                    debits numeric(18,2);
                    credits numeric(18,2);
                BEGIN
                    SELECT COALESCE(SUM(""DebitAmountBase""), 0), COALESCE(SUM(""CreditAmountBase""), 0)
                      INTO debits, credits
                      FROM acc.""JournalLedger""
                     WHERE ""OrgId"" = NEW.""OrgId"";

                    IF debits <> credits THEN
                        RAISE EXCEPTION
                            'The branch ledger does not balance: debits %, credits %',
                            debits, credits;
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                ");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_ledger_balanced ON acc.\"JournalLedger\";");
            
            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_ledger_balanced
                AFTER INSERT OR UPDATE OR DELETE ON acc.""JournalLedger""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_ledger_balanced();
                ");

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

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_journal_balanced ON acc.\"JournalDetails\";");
            
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

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_journal_balanced_on_post ON acc.\"Journals\";");
            
            migrationBuilder.Sql(@"
                CREATE CONSTRAINT TRIGGER trg_journal_balanced_on_post
                AFTER INSERT OR UPDATE ON acc.""Journals""
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION acc.assert_journal_balanced_on_post();
                ");

            foreach (var doc in new[]
            {
                new { parent = "SpendMoney", child = "SpendMoneyDetails", key = "SpendMoneyId" },
                new { parent = "ReceiveMoney", child = "ReceiveMoneyDetails", key = "ReceiveMoneyId" }
            })
            {
                string fn = doc.parent.ToLowerInvariant();

                migrationBuilder.Sql($@"
                    CREATE OR REPLACE FUNCTION acc.assert_{fn}_allocated() RETURNS trigger AS $$
                    DECLARE
                        doc bigint;
                        state varchar(10);
                        header numeric(18,2);
                        allocated numeric(18,2);
                    BEGIN
                        IF TG_OP = 'DELETE' THEN
                            doc := OLD.""{doc.key}"";
                        ELSE
                            doc := NEW.""{doc.key}"";
                        END IF;

                        SELECT ""Status"", ""Amount"" INTO state, header
                          FROM acc.""{doc.parent}"" WHERE ""{doc.key}"" = doc;

                        IF state IS NULL OR state = 'Draft' THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM(""Amount""), 0) INTO allocated
                          FROM acc.""{doc.child}"" WHERE ""{doc.key}"" = doc;

                        IF allocated <> header THEN
                            RAISE EXCEPTION
                                '{doc.parent} % is allocated %, but its amount is %',
                                doc, allocated, header;
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");

                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated ON acc.\"{doc.child}\";");

                migrationBuilder.Sql($@"
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated
                    AFTER INSERT OR UPDATE OR DELETE ON acc.""{doc.child}""
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
                          FROM acc.""{doc.child}"" WHERE ""{doc.key}"" = NEW.""{doc.key}"";

                        IF allocated <> NEW.""Amount"" THEN
                            RAISE EXCEPTION
                                '{doc.parent} % is allocated %, but its amount is %',
                                NEW.""{doc.key}"", allocated, NEW.""Amount"";
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");

                migrationBuilder.Sql(
                    $"DROP TRIGGER IF EXISTS trg_{fn}_allocated_on_post ON acc.\"{doc.parent}\";");

                migrationBuilder.Sql($@"
                    CREATE CONSTRAINT TRIGGER trg_{fn}_allocated_on_post
                    AFTER INSERT OR UPDATE ON acc.""{doc.parent}""
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION acc.assert_{fn}_allocated_on_post();
                    ");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
