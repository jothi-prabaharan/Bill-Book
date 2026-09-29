using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class TdsLowerDeductionCertificate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LowerDeductionCertificateNo",
                schema: "con",
                table: "Contacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowerDeductionLimit",
                schema: "con",
                table: "Contacts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowerDeductionRate",
                schema: "con",
                table: "Contacts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LowerDeductionValidFrom",
                schema: "con",
                table: "Contacts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LowerDeductionValidTo",
                schema: "con",
                table: "Contacts",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LowerDeductionCertificateNo",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "LowerDeductionLimit",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "LowerDeductionRate",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "LowerDeductionValidFrom",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "LowerDeductionValidTo",
                schema: "con",
                table: "Contacts");
        }
    }
}
