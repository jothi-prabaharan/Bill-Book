using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Master
{
    /// <inheritdoc />
    public partial class AddDeliveryChallanTransactionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "TransactionTypes",
                columns: new[] { "Code", "CreatedAt", "CreatedBy", "IsActive", "IsLedgerPosting", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[] { "DLC", null, null, true, true, null, null, "Delivery Challan" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "TransactionTypes",
                keyColumn: "Code",
                keyValue: "DLC");
        }
    }
}
