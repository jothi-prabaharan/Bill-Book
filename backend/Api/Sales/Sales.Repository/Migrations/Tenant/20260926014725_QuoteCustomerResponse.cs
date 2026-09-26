using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class QuoteCustomerResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerResponse",
                schema: "sal",
                table: "Quotes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                // Every existing quote is unanswered. An empty default would
                // fail chk_quotes_response_stamp on the rows already there.
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                schema: "sal",
                table: "Quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RespondedByName",
                schema: "sal",
                table: "Quotes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseNote",
                schema: "sal",
                table: "Quotes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_quotes_response_stamp",
                schema: "sal",
                table: "Quotes",
                sql: "(\"CustomerResponse\" = 'None') = (\"RespondedAt\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_quotes_response_stamp",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomerResponse",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RespondedByName",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ResponseNote",
                schema: "sal",
                table: "Quotes");
        }
    }
}
