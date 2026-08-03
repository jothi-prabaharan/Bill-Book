using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddOverpaymentLedgerSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "mst",
                table: "LedgerSources",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.InsertData(
                schema: "mst",
                table: "LedgerSources",
                columns: new[] { "LedgerSourceId", "Code", "CreatedAt", "CreatedBy", "Direction", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 16, "VENDOROVERPAYMENT", null, null, "Out", true, null, null, "Overpayment to vendor" },
                    { 17, "CUSTOMEROVERPAYMENT", null, null, "In", true, null, null, "Overpayment from customer" },
                    { 18, "CUSTOMEROVERPAYMENTREFUND", null, null, "Out", true, null, null, "Customer overpayment refunded" },
                    { 19, "CUSTOMERPREPAYMENTREFUND", null, null, "Out", true, null, null, "Customer advance refunded" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "LedgerSources",
                keyColumn: "LedgerSourceId",
                keyValue: 16);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "LedgerSources",
                keyColumn: "LedgerSourceId",
                keyValue: 17);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "LedgerSources",
                keyColumn: "LedgerSourceId",
                keyValue: 18);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "LedgerSources",
                keyColumn: "LedgerSourceId",
                keyValue: 19);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "mst",
                table: "LedgerSources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
