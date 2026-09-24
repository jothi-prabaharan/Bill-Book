using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class UserIsPlatformOperator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformOperator",
                schema: "mst",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatformOperator",
                schema: "mst",
                table: "Users");
        }
    }
}
