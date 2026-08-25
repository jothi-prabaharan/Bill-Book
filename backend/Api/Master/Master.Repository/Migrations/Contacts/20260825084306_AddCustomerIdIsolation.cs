using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Contacts
{
    /// <inheritdoc />
    public partial class AddCustomerIdIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_OrgId",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_ContactPersons_OrgId",
                schema: "con",
                table: "ContactPersons");

            migrationBuilder.DropIndex(
                name: "IX_ContactLicences_OrgId",
                schema: "con",
                table: "ContactLicences");

            migrationBuilder.DropIndex(
                name: "IX_ContactBankDetails_OrgId",
                schema: "con",
                table: "ContactBankDetails");

            migrationBuilder.DropIndex(
                name: "IX_ContactAttachments_OrgId",
                schema: "con",
                table: "ContactAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ContactAddresses_OrgId",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropIndex(
                name: "IX_ApiClients_OrgId",
                schema: "con",
                table: "ApiClients");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "Contacts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "ContactPersons",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "ContactPersonRoles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "ContactLicences",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "ContactBankDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "ContactAttachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "ContactAddresses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "con",
                table: "ApiClients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CustomerId_OrgId",
                schema: "con",
                table: "Contacts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_CustomerId_OrgId",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonRoles_CustomerId_OrgId",
                schema: "con",
                table: "ContactPersonRoles",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_CustomerId_OrgId",
                schema: "con",
                table: "ContactLicences",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_CustomerId_OrgId",
                schema: "con",
                table: "ContactBankDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_CustomerId_OrgId",
                schema: "con",
                table: "ContactAttachments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_CustomerId_OrgId",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiClients_CustomerId_OrgId",
                schema: "con",
                table: "ApiClients",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_CustomerId_OrgId",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_ContactPersons_CustomerId_OrgId",
                schema: "con",
                table: "ContactPersons");

            migrationBuilder.DropIndex(
                name: "IX_ContactPersonRoles_CustomerId_OrgId",
                schema: "con",
                table: "ContactPersonRoles");

            migrationBuilder.DropIndex(
                name: "IX_ContactLicences_CustomerId_OrgId",
                schema: "con",
                table: "ContactLicences");

            migrationBuilder.DropIndex(
                name: "IX_ContactBankDetails_CustomerId_OrgId",
                schema: "con",
                table: "ContactBankDetails");

            migrationBuilder.DropIndex(
                name: "IX_ContactAttachments_CustomerId_OrgId",
                schema: "con",
                table: "ContactAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ContactAddresses_CustomerId_OrgId",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropIndex(
                name: "IX_ApiClients_CustomerId_OrgId",
                schema: "con",
                table: "ApiClients");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "ContactPersons");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "ContactPersonRoles");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "ContactLicences");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "ContactBankDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "ContactAttachments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "con",
                table: "ApiClients");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_OrgId",
                schema: "con",
                table: "Contacts",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_OrgId",
                schema: "con",
                table: "ContactPersons",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_OrgId",
                schema: "con",
                table: "ContactLicences",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_OrgId",
                schema: "con",
                table: "ContactBankDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_OrgId",
                schema: "con",
                table: "ContactAttachments",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_OrgId",
                schema: "con",
                table: "ContactAddresses",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiClients_OrgId",
                schema: "con",
                table: "ApiClients",
                column: "OrgId");
        }
    }
}
