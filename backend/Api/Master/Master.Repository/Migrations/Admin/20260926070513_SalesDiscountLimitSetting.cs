using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class SalesDiscountLimitSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000007"), "Sales", "sales.maxLineDiscountPercent", null, null, "Number", "The most a sales line may be discounted without an approved override. A contact's own limit wins; 100 is no limit", true, null, null, "Maximum Line Discount (%)", null, "100" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"));
        }
    }
}
