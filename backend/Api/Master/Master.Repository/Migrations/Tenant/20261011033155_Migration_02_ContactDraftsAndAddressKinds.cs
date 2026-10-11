using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_02_ContactDraftsAndAddressKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_gstin_registration",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_msme",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_tds",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_ContactAddresses_Default",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "AddressType",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "ContactPersonName",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "MobileNumber",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "con",
                table: "ContactAddresses");

            // Dropped rather than renamed (TK-188): the old flag was one default
            // per billing/shipping type, so renaming it would leave two
            // "default shipping" rows on a contact that had both and the new
            // unique index could not be built. No released deployment holds
            // contacts yet; defaults are chosen again on the contact form.
            migrationBuilder.DropColumn(
                name: "IsDefault",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultShipping",
                schema: "con",
                table: "ContactAddresses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                schema: "con",
                table: "Contacts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                schema: "con",
                table: "Contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "ContactAddressId",
                schema: "con",
                table: "ContactPersons",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultBilling",
                schema: "con",
                table: "ContactAddresses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "con",
                table: "ContactAddresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // A value the enum reads, for the rows already there.
                defaultValue: "RegisteredOffice");

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_gstin_registration",
                schema: "con",
                table: "Contacts",
                sql: "\"IsDraft\" = true OR (\"GstRegistrationType\" IN ('Regular', 'Composition', 'Sez') AND \"Gstin\" IS NOT NULL) OR (\"GstRegistrationType\" IN ('Unregistered', 'Overseas', 'Consumer') AND \"Gstin\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_msme",
                schema: "con",
                table: "Contacts",
                sql: "\"IsDraft\" = true OR \"IsMsme\" = false OR \"UdyamNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_tds",
                schema: "con",
                table: "Contacts",
                sql: "\"IsDraft\" = true OR \"IsTdsApplicable\" = false OR \"TdsSection\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_ContactAddressId",
                schema: "con",
                table: "ContactPersons",
                column: "ContactAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_DefaultBilling",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "OrgId", "ContactId" },
                unique: true,
                filter: "\"IsDefaultBilling\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_DefaultShipping",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "OrgId", "ContactId" },
                unique: true,
                filter: "\"IsDefaultShipping\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_OrgId_ContactId",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContactPersons_ContactAddresses_ContactAddressId",
                schema: "con",
                table: "ContactPersons",
                column: "ContactAddressId",
                principalSchema: "con",
                principalTable: "ContactAddresses",
                principalColumn: "ContactAddressId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContactPersons_ContactAddresses_ContactAddressId",
                schema: "con",
                table: "ContactPersons");

            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_gstin_registration",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_msme",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_tds",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_ContactPersons_ContactAddressId",
                schema: "con",
                table: "ContactPersons");

            migrationBuilder.DropIndex(
                name: "IX_ContactAddresses_DefaultBilling",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropIndex(
                name: "IX_ContactAddresses_DefaultShipping",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropIndex(
                name: "IX_ContactAddresses_OrgId_ContactId",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "ContactAddressId",
                schema: "con",
                table: "ContactPersons");

            migrationBuilder.DropColumn(
                name: "IsDefaultBilling",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.DropColumn(
                name: "IsDefaultShipping",
                schema: "con",
                table: "ContactAddresses");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                schema: "con",
                table: "ContactAddresses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                schema: "con",
                table: "Contacts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressType",
                schema: "con",
                table: "ContactAddresses",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Billing");

            migrationBuilder.AddColumn<string>(
                name: "ContactPersonName",
                schema: "con",
                table: "ContactAddresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobileNumber",
                schema: "con",
                table: "ContactAddresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "con",
                table: "ContactAddresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_gstin_registration",
                schema: "con",
                table: "Contacts",
                sql: "(\"GstRegistrationType\" IN ('Regular', 'Composition', 'Sez') AND \"Gstin\" IS NOT NULL) OR (\"GstRegistrationType\" IN ('Unregistered', 'Overseas', 'Consumer') AND \"Gstin\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_msme",
                schema: "con",
                table: "Contacts",
                sql: "\"IsMsme\" = false OR \"UdyamNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_tds",
                schema: "con",
                table: "Contacts",
                sql: "\"IsTdsApplicable\" = false OR \"TdsSection\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_Default",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "OrgId", "ContactId", "AddressType" },
                unique: true,
                filter: "\"IsDefault\" = true");
        }
    }
}
