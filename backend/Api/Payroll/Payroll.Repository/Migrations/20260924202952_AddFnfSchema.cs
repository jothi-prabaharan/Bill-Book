using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Payroll.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddFnfSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "pay",
                table: "PayrollRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FullAndFinalSettlements",
                schema: "pay",
                columns: table => new
                {
                    FullAndFinalSettlementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    SeparationId = table.Column<long>(type: "bigint", nullable: true),
                    LastWorkingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayrollRunId = table.Column<long>(type: "bigint", nullable: true),
                    NetPayable = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_FullAndFinalSettlements", x => x.FullAndFinalSettlementId);
                });

            migrationBuilder.CreateTable(
                name: "FnfLines",
                schema: "pay",
                columns: table => new
                {
                    FnfLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullAndFinalSettlementId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsDeduction = table.Column<bool>(type: "boolean", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_FnfLines", x => x.FnfLineId);
                    table.ForeignKey(
                        name: "FK_FnfLines_FullAndFinalSettlements_FullAndFinalSettlementId",
                        column: x => x.FullAndFinalSettlementId,
                        principalSchema: "pay",
                        principalTable: "FullAndFinalSettlements",
                        principalColumn: "FullAndFinalSettlementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FnfLines_CustomerId_OrgId",
                schema: "pay",
                table: "FnfLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FnfLines_FullAndFinalSettlementId",
                schema: "pay",
                table: "FnfLines",
                column: "FullAndFinalSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_FullAndFinalSettlements_CustomerId_OrgId",
                schema: "pay",
                table: "FullAndFinalSettlements",
                columns: new[] { "CustomerId", "OrgId" });

            string[] rlsTables =
            [
                "FullAndFinalSettlements",
                "FnfLines"
            ];

            foreach (string table in rlsTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE pay."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE pay."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON pay."{table}";
                    CREATE POLICY {policy}
                        ON pay."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FnfLines",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "FullAndFinalSettlements",
                schema: "pay");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "pay",
                table: "PayrollRuns");
        }
    }
}
