using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class PortalLinkDaysSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000006"), "Portal", "portal.linkDays", null, null, "Number", "Days a contact's portal link keeps working, unless it is revoked first", true, null, null, "Portal Link Validity (days)", null, "90" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"));
        }
    }
}
