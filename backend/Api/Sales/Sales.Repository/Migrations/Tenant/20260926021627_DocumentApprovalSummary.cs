using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class DocumentApprovalSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "sal",
                table: "SalesOrders",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "SalesOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "SalesOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "sal",
                table: "Quotes",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "Quotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "Quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "Quotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "sal",
                table: "Invoices",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "Invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "sal",
                table: "DeliveryChallans",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "DeliveryChallans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "DeliveryChallans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "DeliveryChallans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "sal",
                table: "CreditNotes",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "CreditNotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "CreditNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "CreditNotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "sal",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "sal",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "sal",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "sal",
                table: "CreditNotes");
        }
    }
}
