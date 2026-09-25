using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class FeeTransactionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "TransactionTypes",
                columns: new[] { "Code", "CreatedAt", "CreatedBy", "IsActive", "IsLedgerPosting", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { "FDM", null, null, true, true, null, null, "Fee Demand" },
                    { "FRC", null, null, true, true, null, null, "Fee Receipt" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "TransactionTypes",
                keyColumn: "Code",
                keyValue: "FDM");

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "TransactionTypes",
                keyColumn: "Code",
                keyValue: "FRC");
        }
    }
}
