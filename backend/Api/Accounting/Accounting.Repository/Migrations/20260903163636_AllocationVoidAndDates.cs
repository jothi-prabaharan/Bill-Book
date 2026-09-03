using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AllocationVoidAndDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "acc",
                table: "TransactionRatios",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<DateOnly>(
                name: "AllocationDate",
                schema: "acc",
                table: "TransactionRatios",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Rows written before the effective date existed take it from the
            // stamp that recorded when they were applied. Left at the scaffolded
            // 0001-01-01 every existing allocation would sort before the books
            // began and report in no period at all.
            migrationBuilder.Sql(
                "UPDATE acc.\"TransactionRatios\" SET \"AllocationDate\" = \"AllocatedAt\"::date "
                    + "WHERE \"AllocationDate\" = DATE '0001-01-01';");

            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                schema: "acc",
                table: "TransactionRatios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "acc",
                table: "TransactionRatios",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                schema: "acc",
                table: "TransactionRatios",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                schema: "acc",
                table: "TransactionRatios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllocationDate",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "acc",
                table: "TransactionRatios",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }
    }
}
