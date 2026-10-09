using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class BillTds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TdsAmount",
                schema: "pur",
                table: "Bills",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsBase",
                schema: "pur",
                table: "Bills",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TdsNote",
                schema: "pur",
                table: "Bills",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TdsSectionCode",
                schema: "pur",
                table: "Bills",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TdsAmount",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TdsBase",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TdsNote",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TdsSectionCode",
                schema: "pur",
                table: "Bills");
        }
    }
}
