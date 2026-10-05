using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class LoyaltyEarnRateSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000012"), "Restaurant", "restaurant.loyalty.earnRate", null, null, "Number", "Points a posted bill earns the customer it names, per ₹100 of its total, rounded down. 0 earns nothing.", true, null, null, "Loyalty points per ₹100", null, "0" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000012"));
        }
    }
}
