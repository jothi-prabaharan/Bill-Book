using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                schema: "acc",
                columns: table => new
                {
                    OpeningBalanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Memo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.OpeningBalanceId);
                    table.CheckConstraint("chk_opening_finalized_stamp", "(\"Status\" = 'Draft') = (\"FinalizedAt\" IS NULL)");
                    table.CheckConstraint("chk_opening_number_on_finalize", "(\"Status\" = 'Draft' AND \"TransactionNo\" IS NULL) OR (\"Status\" <> 'Draft' AND \"TransactionNo\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalanceLines",
                schema: "acc",
                columns: table => new
                {
                    OpeningBalanceLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OpeningBalanceId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    LineType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    DebitAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_OpeningBalanceLines", x => x.OpeningBalanceLineId);
                    table.CheckConstraint("chk_opening_line_exclusive", "\"LineType\" = 'Item' OR (\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");
                    table.CheckConstraint("chk_opening_line_item_shape", "(\"LineType\" = 'Item' AND \"Quantity\" > 0 AND \"UnitCost\" >= 0 AND \"DebitAmount\" = 0 AND \"CreditAmount\" = 0) OR (\"LineType\" <> 'Item' AND \"Quantity\" IS NULL AND \"UnitCost\" IS NULL)");
                    table.CheckConstraint("chk_opening_line_names_its_subject", "(\"LineType\" = 'GlAccount' AND \"AccountId\" IS NOT NULL AND \"ContactId\" IS NULL AND \"ItemId\" IS NULL) OR (\"LineType\" IN ('ContactReceivable', 'ContactPayable') AND \"ContactId\" IS NOT NULL AND \"AccountId\" IS NULL AND \"ItemId\" IS NULL) OR (\"LineType\" = 'Item' AND \"ItemId\" IS NOT NULL AND \"AccountId\" IS NULL AND \"ContactId\" IS NULL)");
                    table.CheckConstraint("chk_opening_line_non_negative", "\"DebitAmount\" >= 0 AND \"CreditAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                        column: x => x.OpeningBalanceId,
                        principalSchema: "acc",
                        principalTable: "OpeningBalances",
                        principalColumn: "OpeningBalanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_AccountId",
                schema: "acc",
                table: "OpeningBalanceLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OpeningBalanceId_LineNumber",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "OpeningBalanceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OrgId",
                schema: "acc",
                table: "OpeningBalanceLines",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OrgId_ContactId",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OrgId_ItemId",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_Org",
                schema: "acc",
                table: "OpeningBalances",
                column: "OrgId",
                unique: true);

            // Row-level security, as on every other per-customer table. This one
            // holds a branch's entire starting position — every balance it has
            // ever reported is measured from these rows — so it is the last table
            // where the EF query filter should be the only thing standing between
            // two branches.
            foreach (string table in new[] { "OpeningBalances", "OpeningBalanceLines" })
            {
                string policy = table.ToLowerInvariant() + "_org_isolation";

                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {policy} ON acc.\"{table}\";");
                migrationBuilder.Sql(
                    $"CREATE POLICY {policy} ON acc.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[] { "OpeningBalances", "OpeningBalanceLines" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation "
                        + $"ON acc.\"{table}\";");
            }

            migrationBuilder.DropTable(
                name: "OpeningBalanceLines",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "OpeningBalances",
                schema: "acc");
        }
    }
}
