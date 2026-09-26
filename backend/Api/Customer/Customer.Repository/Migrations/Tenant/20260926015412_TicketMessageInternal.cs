using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class TicketMessageInternal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                schema: "cus",
                table: "TicketMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "chk_ticketmessages_internal_by_staff",
                schema: "cus",
                table: "TicketMessages",
                sql: "NOT \"IsInternal\" OR \"AuthorType\" = 'User'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_ticketmessages_internal_by_staff",
                schema: "cus",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                schema: "cus",
                table: "TicketMessages");
        }
    }
}
