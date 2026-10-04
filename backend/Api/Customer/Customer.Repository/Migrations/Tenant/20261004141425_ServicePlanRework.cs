using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ServicePlanRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Every",
                schema: "cus",
                table: "ServicePlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastServicedOn",
                schema: "cus",
                table: "ServicePlans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastTicketId",
                schema: "cus",
                table: "ServicePlans",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "cus",
                table: "ServicePlans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "cus",
                table: "ServicePlans",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlans_OrgId_ContactId",
                schema: "cus",
                table: "ServicePlans",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlans_OrgId_Status_NextDueDate",
                schema: "cus",
                table: "ServicePlans",
                columns: new[] { "OrgId", "Status", "NextDueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServicePlans_OrgId_ContactId",
                schema: "cus",
                table: "ServicePlans");

            migrationBuilder.DropIndex(
                name: "IX_ServicePlans_OrgId_Status_NextDueDate",
                schema: "cus",
                table: "ServicePlans");

            migrationBuilder.DropColumn(
                name: "Every",
                schema: "cus",
                table: "ServicePlans");

            migrationBuilder.DropColumn(
                name: "LastServicedOn",
                schema: "cus",
                table: "ServicePlans");

            migrationBuilder.DropColumn(
                name: "LastTicketId",
                schema: "cus",
                table: "ServicePlans");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "cus",
                table: "ServicePlans");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "cus",
                table: "ServicePlans");
        }
    }
}
