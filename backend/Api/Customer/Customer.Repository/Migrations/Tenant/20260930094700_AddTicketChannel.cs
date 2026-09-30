using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddTicketChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                schema: "cus",
                table: "Tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Web");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "cus",
                table: "Tickets");
        }
    }
}
