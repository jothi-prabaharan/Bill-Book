using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionRatios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionRatios",
                schema: "acc",
                columns: table => new
                {
                    TransactionRatioId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    TargetTransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TargetTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionRatios", x => x.TransactionRatioId);
                    table.CheckConstraint("chk_transactionratio_amount", "\"Amount\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_OrgId",
                schema: "acc",
                table: "TransactionRatios",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_OrgId_SourceTransactionTypeCode_SourceTra~",
                schema: "acc",
                table: "TransactionRatios",
                columns: new[] { "OrgId", "SourceTransactionTypeCode", "SourceTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_OrgId_TargetTransactionTypeCode_TargetTra~",
                schema: "acc",
                table: "TransactionRatios",
                columns: new[] { "OrgId", "TargetTransactionTypeCode", "TargetTransactionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionRatios",
                schema: "acc");
        }
    }
}
