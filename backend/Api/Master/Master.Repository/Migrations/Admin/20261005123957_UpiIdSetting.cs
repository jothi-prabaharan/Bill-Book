using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class UpiIdSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000011"), "Payments", "payment.upiId", null, null, "Text", "The UPI id customers pay this branch at, such as shop@okhdfcbank. An unpaid invoice prints a QR code for it; leave empty for none", true, null, null, "UPI ID", null, "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000011"));
        }
    }
}
