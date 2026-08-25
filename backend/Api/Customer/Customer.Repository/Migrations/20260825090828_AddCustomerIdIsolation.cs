using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_OrgId",
                schema: "cus",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_OrgId",
                schema: "cus",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_Leads_OrgId",
                schema: "cus",
                table: "Leads");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "cus",
                table: "Tickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "cus",
                table: "TicketMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "cus",
                table: "Leads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CustomerId_OrgId",
                schema: "cus",
                table: "Tickets",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_CustomerId_OrgId",
                schema: "cus",
                table: "TicketMessages",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CustomerId_OrgId",
                schema: "cus",
                table: "Leads",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_CustomerId_OrgId",
                schema: "cus",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketMessages_CustomerId_OrgId",
                schema: "cus",
                table: "TicketMessages");

            migrationBuilder.DropIndex(
                name: "IX_Leads_CustomerId_OrgId",
                schema: "cus",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "cus",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "cus",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "cus",
                table: "Leads");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrgId",
                schema: "cus",
                table: "Tickets",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_OrgId",
                schema: "cus",
                table: "TicketMessages",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OrgId",
                schema: "cus",
                table: "Leads",
                column: "OrgId");
        }
    }
}
