using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class RestaurantOrderTransactionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "TransactionTypes",
                columns: new[] { "Code", "CreatedAt", "CreatedBy", "IsActive", "IsLedgerPosting", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[] { "RST", null, null, true, false, null, null, "Restaurant Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "TransactionTypes",
                keyColumn: "Code",
                keyValue: "RST");
        }
    }
}
