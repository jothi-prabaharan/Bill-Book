using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class GdniLedgerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "LedgerTypes",
                columns: new[] { "LedgerTypeId", "Code", "CreatedAt", "CreatedBy", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[] { 7, "GDNI", null, null, true, null, null, "Goods delivered not invoiced clearing" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "LedgerTypes",
                keyColumn: "LedgerTypeId",
                keyValue: 7);
        }
    }
}
