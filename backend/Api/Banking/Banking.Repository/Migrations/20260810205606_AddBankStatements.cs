using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Banking.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBankStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankStatements",
                schema: "bnk",
                columns: table => new
                {
                    BankStatementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    StatementReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ClosingBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImportedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatements", x => x.BankStatementId);
                    table.CheckConstraint("chk_statement_period", "\"ToDate\" >= \"FromDate\"");
                    table.ForeignKey(
                        name: "FK_BankStatements_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatementImportProfiles",
                schema: "bnk",
                columns: table => new
                {
                    StatementImportProfileId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    SkipRows = table.Column<int>(type: "integer", nullable: false),
                    HasHeaderRow = table.Column<bool>(type: "boolean", nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DateColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueDateColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DescriptionColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WithdrawalColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepositColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AmountColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NegativeIsDeposit = table.Column<bool>(type: "boolean", nullable: false),
                    BalanceColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementImportProfiles", x => x.StatementImportProfileId);
                    table.CheckConstraint("chk_import_profile_amount_shape", "(\"WithdrawalColumn\" IS NOT NULL AND \"DepositColumn\" IS NOT NULL AND \"AmountColumn\" IS NULL) OR (\"AmountColumn\" IS NOT NULL AND \"WithdrawalColumn\" IS NULL AND \"DepositColumn\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_StatementImportProfiles_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "bnk",
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                schema: "bnk",
                columns: table => new
                {
                    BankStatementLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankStatementId = table.Column<long>(type: "bigint", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WithdrawalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RunningBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    MatchedTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MatchedTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    MatchedAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MatchedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RowHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.BankStatementLineId);
                    table.CheckConstraint("chk_statement_line_exclusive", "(\"WithdrawalAmount\" > 0 AND \"DepositAmount\" = 0) OR (\"DepositAmount\" > 0 AND \"WithdrawalAmount\" = 0)");
                    table.CheckConstraint("chk_statement_line_ignored_reason", "\"Status\" <> 'Ignored' OR \"Note\" IS NOT NULL");
                    table.CheckConstraint("chk_statement_line_match", "(\"Status\" = 'Matched' AND \"MatchedTransactionTypeCode\" IS NOT NULL AND \"MatchedTransactionId\" IS NOT NULL AND \"MatchedAt\" IS NOT NULL) OR (\"Status\" <> 'Matched' AND \"MatchedTransactionTypeCode\" IS NULL AND \"MatchedTransactionId\" IS NULL AND \"MatchedAt\" IS NULL)");
                    table.CheckConstraint("chk_statement_line_matched_type", "\"MatchedTransactionTypeCode\" IS NULL OR \"MatchedTransactionTypeCode\" IN ('SPM', 'RCM', 'TRM')");
                    table.CheckConstraint("chk_statement_line_non_negative", "\"WithdrawalAmount\" >= 0 AND \"DepositAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankStatements_BankStatementId",
                        column: x => x.BankStatementId,
                        principalSchema: "bnk",
                        principalTable: "BankStatements",
                        principalColumn: "BankStatementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_BankStatementId_LineNumber",
                schema: "bnk",
                table: "BankStatementLines",
                columns: new[] { "BankStatementId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_OrgId",
                schema: "bnk",
                table: "BankStatementLines",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_OrgId_BankAccountId_Status_TransactionDa~",
                schema: "bnk",
                table: "BankStatementLines",
                columns: new[] { "OrgId", "BankAccountId", "Status", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_Row",
                schema: "bnk",
                table: "BankStatementLines",
                columns: new[] { "OrgId", "BankAccountId", "RowHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_BankAccountId",
                schema: "bnk",
                table: "BankStatements",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_OrgId",
                schema: "bnk",
                table: "BankStatements",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_OrgId_BankAccountId_FromDate",
                schema: "bnk",
                table: "BankStatements",
                columns: new[] { "OrgId", "BankAccountId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_Account",
                schema: "bnk",
                table: "StatementImportProfiles",
                columns: new[] { "OrgId", "BankAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_BankAccountId",
                schema: "bnk",
                table: "StatementImportProfiles",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_OrgId",
                schema: "bnk",
                table: "StatementImportProfiles",
                column: "OrgId");

            // Row-level security, as on every other per-customer table. A bank
            // statement is one branch's account of its own money, and the
            // narration on it names counterparties and references — so it is
            // exactly the kind of table where the EF query filter should not be
            // the only thing standing between two branches.
            foreach (string table in new[]
            {
                "BankStatements", "BankStatementLines", "StatementImportProfiles",
            })
            {
                string policy = table.ToLowerInvariant() + "_org_isolation";

                migrationBuilder.Sql($"ALTER TABLE bnk.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {policy} ON bnk.\"{table}\";");
                migrationBuilder.Sql(
                    $"CREATE POLICY {policy} ON bnk.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "BankStatements", "BankStatementLines", "StatementImportProfiles",
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation "
                        + $"ON bnk.\"{table}\";");
            }

            migrationBuilder.DropTable(
                name: "BankStatementLines",
                schema: "bnk");

            migrationBuilder.DropTable(
                name: "StatementImportProfiles",
                schema: "bnk");

            migrationBuilder.DropTable(
                name: "BankStatements",
                schema: "bnk");
        }
    }
}
