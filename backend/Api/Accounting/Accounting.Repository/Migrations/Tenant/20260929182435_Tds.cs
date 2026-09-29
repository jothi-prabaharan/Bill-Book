using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Tds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BsrCode",
                schema: "acc",
                table: "SpendMoney",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ChallanMonth",
                schema: "acc",
                table: "SpendMoney",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChallanNo",
                schema: "acc",
                table: "SpendMoney",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChallanSectionCode",
                schema: "acc",
                table: "SpendMoney",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTdsChallan",
                schema: "acc",
                table: "SpendMoney",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsAmount",
                schema: "acc",
                table: "SpendMoney",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TdsSectionCode",
                schema: "acc",
                table: "SpendMoney",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeductorTan",
                schema: "acc",
                table: "ReceiveMoney",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsAmount",
                schema: "acc",
                table: "ReceiveMoney",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TdsSectionCode",
                schema: "acc",
                table: "ReceiveMoney",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Form26asImports",
                schema: "acc",
                columns: table => new
                {
                    Form26asImportId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FinancialYear = table.Column<int>(type: "integer", nullable: false),
                    Pan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    MatchedCount = table.Column<int>(type: "integer", nullable: false),
                    DifferenceCount = table.Column<int>(type: "integer", nullable: false),
                    MissingInBooksCount = table.Column<int>(type: "integer", nullable: false),
                    MissingIn26asCount = table.Column<int>(type: "integer", nullable: false),
                    CreditAtRisk = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Form26asImports", x => x.Form26asImportId);
                });

            migrationBuilder.CreateTable(
                name: "TdsEntries",
                schema: "acc",
                columns: table => new
                {
                    TdsEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TransactionId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FinancialYear = table.Column<int>(type: "integer", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    SectionCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Pan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Tan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DeducteeKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CoveredByAdvance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TdsBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(7,3)", nullable: false),
                    CertificateNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CertificateBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CertificateRate = table.Column<decimal>(type: "numeric(7,3)", nullable: true),
                    TdsAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChallanSpendMoneyId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_TdsEntries", x => x.TdsEntryId);
                    table.ForeignKey(
                        name: "FK_TdsEntries_SpendMoney_ChallanSpendMoneyId",
                        column: x => x.ChallanSpendMoneyId,
                        principalSchema: "acc",
                        principalTable: "SpendMoney",
                        principalColumn: "SpendMoneyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TdsSections",
                schema: "acc",
                columns: table => new
                {
                    TdsSectionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NatureOfPayment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    RateIndividual = table.Column<decimal>(type: "numeric(7,3)", nullable: false),
                    RateOther = table.Column<decimal>(type: "numeric(7,3)", nullable: false),
                    RateNoPan = table.Column<decimal>(type: "numeric(7,3)", nullable: false),
                    SingleThreshold = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AggregateThreshold = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ThresholdBasis = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TdsSections", x => x.TdsSectionId);
                    table.CheckConstraint("chk_tdssections_period", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                });

            migrationBuilder.CreateTable(
                name: "Form26asLines",
                schema: "acc",
                columns: table => new
                {
                    Form26asLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Form26asImportId = table.Column<long>(type: "bigint", nullable: false),
                    MatchState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeductorTan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeductorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SectionCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StatementAmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    StatementTds = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BooksTds = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ReceiveMoneyId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_Form26asLines", x => x.Form26asLineId);
                    table.ForeignKey(
                        name: "FK_Form26asLines_Form26asImports_Form26asImportId",
                        column: x => x.Form26asImportId,
                        principalSchema: "acc",
                        principalTable: "Form26asImports",
                        principalColumn: "Form26asImportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "chk_spendmoney_challan",
                schema: "acc",
                table: "SpendMoney",
                sql: "\"IsTdsChallan\" = (\"ChallanSectionCode\" IS NOT NULL AND \"ChallanMonth\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_spendmoney_tds_nonnegative",
                schema: "acc",
                table: "SpendMoney",
                sql: "\"TdsAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_receivemoney_tds_nonnegative",
                schema: "acc",
                table: "ReceiveMoney",
                sql: "\"TdsAmount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Form26asImports_CustomerId_OrgId",
                schema: "acc",
                table: "Form26asImports",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Form26asImports_OrgId_FinancialYear",
                schema: "acc",
                table: "Form26asImports",
                columns: new[] { "OrgId", "FinancialYear" });

            migrationBuilder.CreateIndex(
                name: "IX_Form26asLines_CustomerId_OrgId",
                schema: "acc",
                table: "Form26asLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Form26asLines_Form26asImportId",
                schema: "acc",
                table: "Form26asLines",
                column: "Form26asImportId");

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntries_ChallanSpendMoneyId",
                schema: "acc",
                table: "TdsEntries",
                column: "ChallanSpendMoneyId");

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntries_CustomerId_OrgId",
                schema: "acc",
                table: "TdsEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntries_OrgId_ChallanSpendMoneyId",
                schema: "acc",
                table: "TdsEntries",
                columns: new[] { "OrgId", "ChallanSpendMoneyId" },
                filter: "\"ChallanSpendMoneyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntries_OrgId_Direction_ContactId_SectionCode_FinancialY~",
                schema: "acc",
                table: "TdsEntries",
                columns: new[] { "OrgId", "Direction", "ContactId", "SectionCode", "FinancialYear" });

            migrationBuilder.CreateIndex(
                name: "IX_TdsEntries_OrgId_TransactionTypeCode_TransactionId",
                schema: "acc",
                table: "TdsEntries",
                columns: new[] { "OrgId", "TransactionTypeCode", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TdsSections_CustomerId_OrgId",
                schema: "acc",
                table: "TdsSections",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TdsSections_OrgId_SectionCode_EffectiveFrom",
                schema: "acc",
                table: "TdsSections",
                columns: new[] { "OrgId", "SectionCode", "EffectiveFrom" },
                unique: true);

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-117).
            foreach (string table in new[] { "TdsSections", "TdsEntries", "Form26asImports", "Form26asLines" })
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";
                migrationBuilder.Sql($"""
                    ALTER TABLE acc."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE acc."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON acc."{table}";
                    CREATE POLICY {policy}
                        ON acc."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }

            // A posted money document's lines add up to its amount plus the TDS
            // withheld from it, and a TDS challan has no lines at all (TK-117).
            // Replaces the functions RestoreLedgerTriggers wrote; the triggers
            // that call them are unchanged.
            foreach ((string parent, string child, string key, bool challans) in AllocatedDocuments)
            {
                string fn = parent.ToLowerInvariant();
                string challanSkip = challans ? "OR is_challan" : string.Empty;
                string challanColumn = challans ? ", \"IsTdsChallan\"" : ", false";

                migrationBuilder.Sql($@"
                    CREATE OR REPLACE FUNCTION acc.assert_{fn}_allocated() RETURNS trigger AS $$
                    DECLARE
                        doc bigint;
                        state varchar(10);
                        header numeric(18,2);
                        is_challan boolean;
                        allocated numeric(18,2);
                    BEGIN
                        IF TG_OP = 'DELETE' THEN
                            doc := OLD.""{key}"";
                        ELSE
                            doc := NEW.""{key}"";
                        END IF;

                        SELECT ""Status"", ""Amount"" + ""TdsAmount""{challanColumn} INTO state, header, is_challan
                          FROM acc.""{parent}"" WHERE ""{key}"" = doc;

                        IF state IS NULL OR state = 'Draft' {challanSkip} THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM(""Amount""), 0) INTO allocated
                          FROM acc.""{child}"" WHERE ""{key}"" = doc;

                        IF allocated <> header THEN
                            RAISE EXCEPTION
                                '{parent} % is allocated %, but its amount with TDS is %',
                                doc, allocated, header;
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");

                string postSkip = challans ? "OR NEW.\"IsTdsChallan\"" : string.Empty;

                migrationBuilder.Sql($@"
                    CREATE OR REPLACE FUNCTION acc.assert_{fn}_allocated_on_post() RETURNS trigger AS $$
                    DECLARE
                        allocated numeric(18,2);
                    BEGIN
                        IF NEW.""Status"" = 'Draft' {postSkip} THEN
                            RETURN NULL;
                        END IF;

                        SELECT COALESCE(SUM(""Amount""), 0) INTO allocated
                          FROM acc.""{child}"" WHERE ""{key}"" = NEW.""{key}"";

                        IF allocated <> NEW.""Amount"" + NEW.""TdsAmount"" THEN
                            RAISE EXCEPTION
                                '{parent} % is allocated %, but its amount with TDS is %',
                                NEW.""{key}"", allocated, NEW.""Amount"" + NEW.""TdsAmount"";
                        END IF;

                        RETURN NULL;
                    END;
                    $$ LANGUAGE plpgsql;
                    ");
            }
        }

        private static readonly (string Parent, string Child, string Key, bool Challans)[] AllocatedDocuments =
        [
            ("SpendMoney", "SpendMoneyDetails", "SpendMoneyId", true),
            ("ReceiveMoney", "ReceiveMoneyDetails", "ReceiveMoneyId", false),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to RestoreLedgerTriggers' functions before the columns they
            // no longer read are dropped.
            foreach ((string parent, string child, string key, _) in AllocatedDocuments)
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
            }

            migrationBuilder.DropTable(
                name: "Form26asLines",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "TdsEntries",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "TdsSections",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Form26asImports",
                schema: "acc");

            migrationBuilder.DropCheckConstraint(
                name: "chk_spendmoney_challan",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropCheckConstraint(
                name: "chk_spendmoney_tds_nonnegative",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropCheckConstraint(
                name: "chk_receivemoney_tds_nonnegative",
                schema: "acc",
                table: "ReceiveMoney");

            migrationBuilder.DropColumn(
                name: "BsrCode",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "ChallanMonth",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "ChallanNo",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "ChallanSectionCode",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "IsTdsChallan",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "TdsAmount",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "TdsSectionCode",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "DeductorTan",
                schema: "acc",
                table: "ReceiveMoney");

            migrationBuilder.DropColumn(
                name: "TdsAmount",
                schema: "acc",
                table: "ReceiveMoney");

            migrationBuilder.DropColumn(
                name: "TdsSectionCode",
                schema: "acc",
                table: "ReceiveMoney");
        }
    }
}
