using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Printing.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperSizeAndIsSampleToPrintTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSample",
                schema: "prt",
                table: "PrintTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaperSizeName",
                schema: "prt",
                table: "PrintTemplates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSample",
                schema: "prt",
                table: "PrintTemplates");

            migrationBuilder.DropColumn(
                name: "PaperSizeName",
                schema: "prt",
                table: "PrintTemplates");
        }
    }
}
