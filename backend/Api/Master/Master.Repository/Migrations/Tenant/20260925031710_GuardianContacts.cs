using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class GuardianContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_role",
                schema: "con",
                table: "Contacts");

            migrationBuilder.AddColumn<bool>(
                name: "IsGuardian",
                schema: "con",
                table: "Contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Guardian",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsGuardian\" = true");

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_role",
                schema: "con",
                table: "Contacts",
                sql: "\"IsCustomer\" = true OR \"IsVendor\" = true OR \"IsJobWorker\" = true OR \"IsPrescriber\" = true OR \"IsGuardian\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_Guardian",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropCheckConstraint(
                name: "chk_contact_role",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "IsGuardian",
                schema: "con",
                table: "Contacts");

            migrationBuilder.AddCheckConstraint(
                name: "chk_contact_role",
                schema: "con",
                table: "Contacts",
                sql: "\"IsCustomer\" = true OR \"IsVendor\" = true OR \"IsJobWorker\" = true OR \"IsPrescriber\" = true");
        }
    }
}
