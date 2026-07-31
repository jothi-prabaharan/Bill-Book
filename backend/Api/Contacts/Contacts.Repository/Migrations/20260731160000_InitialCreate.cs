using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Contacts.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "con");

            migrationBuilder.CreateTable(
                name: "Contacts",
                schema: "con",
                columns: table => new
                {
                    ContactId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsCustomer = table.Column<bool>(type: "boolean", nullable: false),
                    IsVendor = table.Column<bool>(type: "boolean", nullable: false),
                    IsJobWorker = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrescriber = table.Column<bool>(type: "boolean", nullable: false),
                    ContactCategory = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    GstRegistrationType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Pan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Tan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: true),
                    CountryId = table.Column<int>(type: "integer", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentTermId = table.Column<long>(type: "bigint", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaxOutstandingDays = table.Column<int>(type: "integer", nullable: true),
                    MaxDiscountPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ReceivableAccountId = table.Column<long>(type: "bigint", nullable: true),
                    PayableAccountId = table.Column<long>(type: "bigint", nullable: true),
                    IsTdsApplicable = table.Column<bool>(type: "boolean", nullable: false),
                    TdsSection = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsMsme = table.Column<bool>(type: "boolean", nullable: false),
                    UdyamNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.ContactId);
                    table.CheckConstraint("chk_contact_gstin_registration", "(\"GstRegistrationType\" IN ('Regular', 'Composition', 'Sez') AND \"Gstin\" IS NOT NULL) OR (\"GstRegistrationType\" IN ('Unregistered', 'Overseas', 'Consumer') AND \"Gstin\" IS NULL)");
                    table.CheckConstraint("chk_contact_limits", "(\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0) AND (\"MaxOutstandingDays\" IS NULL OR \"MaxOutstandingDays\" >= 0) AND (\"MaxDiscountPercent\" IS NULL OR (\"MaxDiscountPercent\" >= 0 AND \"MaxDiscountPercent\" <= 100))");
                    table.CheckConstraint("chk_contact_msme", "\"IsMsme\" = false OR \"UdyamNumber\" IS NOT NULL");
                    table.CheckConstraint("chk_contact_role", "\"IsCustomer\" = true OR \"IsVendor\" = true OR \"IsJobWorker\" = true OR \"IsPrescriber\" = true");
                    table.CheckConstraint("chk_contact_tds", "\"IsTdsApplicable\" = false OR \"TdsSection\" IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "ContactPersonRoles",
                schema: "con",
                columns: table => new
                {
                    ContactPersonRoleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleSystemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RoleName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPersonRoles", x => x.ContactPersonRoleId);
                });

            migrationBuilder.CreateTable(
                name: "ContactAddresses",
                schema: "con",
                columns: table => new
                {
                    ContactAddressId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    AddressType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Landmark = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StateId = table.Column<int>(type: "integer", nullable: true),
                    CountryId = table.Column<int>(type: "integer", nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ContactPersonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactAddresses", x => x.ContactAddressId);
                    table.ForeignKey(
                        name: "FK_ContactAddresses_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "con",
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactPersons",
                schema: "con",
                columns: table => new
                {
                    ContactPersonId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactPersonRoleId = table.Column<long>(type: "bigint", nullable: false),
                    Salutation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPersons", x => x.ContactPersonId);
                    table.ForeignKey(
                        name: "FK_ContactPersons_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "con",
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Cascade);
                    // Restrict, not cascade: deleting a role must never delete
                    // the people who hold it.
                    table.ForeignKey(
                        name: "FK_ContactPersons_ContactPersonRoles_ContactPersonRoleId",
                        column: x => x.ContactPersonRoleId,
                        principalSchema: "con",
                        principalTable: "ContactPersonRoles",
                        principalColumn: "ContactPersonRoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_OrgId",
                schema: "con",
                table: "Contacts",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_OrgId_ContactCode",
                schema: "con",
                table: "Contacts",
                columns: new[] { "OrgId", "ContactCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_OrgId_DisplayName",
                schema: "con",
                table: "Contacts",
                columns: new[] { "OrgId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Gstin",
                schema: "con",
                table: "Contacts",
                columns: new[] { "OrgId", "Gstin" },
                unique: true,
                filter: "\"Gstin\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Customer",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsCustomer\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Vendor",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsVendor\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_JobWorker",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsJobWorker\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Prescriber",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsPrescriber\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_ContactId",
                schema: "con",
                table: "ContactAddresses",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_OrgId",
                schema: "con",
                table: "ContactAddresses",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_OrgId_ContactId_AddressType",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "OrgId", "ContactId", "AddressType" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_Default",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "OrgId", "ContactId", "AddressType" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_ContactId",
                schema: "con",
                table: "ContactPersons",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_ContactPersonRoleId",
                schema: "con",
                table: "ContactPersons",
                column: "ContactPersonRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_OrgId",
                schema: "con",
                table: "ContactPersons",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_OrgId_ContactId",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_Default",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "OrgId", "ContactId" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_Mobile",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "OrgId", "MobileNumber" },
                filter: "\"MobileNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_Email",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "OrgId", "Email" },
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonRoles_OrgId",
                schema: "con",
                table: "ContactPersonRoles",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonRoles_OrgId_RoleName",
                schema: "con",
                table: "ContactPersonRoles",
                columns: new[] { "OrgId", "RoleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonRoles_SystemName",
                schema: "con",
                table: "ContactPersonRoles",
                columns: new[] { "OrgId", "RoleSystemName" },
                unique: true,
                filter: "\"RoleSystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonRoles_Default",
                schema: "con",
                table: "ContactPersonRoles",
                column: "OrgId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonRoles_Order",
                schema: "con",
                table: "ContactPersonRoles",
                columns: new[] { "OrgId", "DisplayOrder", "RoleName" });

            // Row-level security on every con table. The EF query filter is the
            // first line of defence; this is the one that holds if a query ever
            // runs without it. One of the four raw-SQL exceptions CLAUDE.md allows.
            foreach (string table in new[]
                { "Contacts", "ContactAddresses", "ContactPersons", "ContactPersonRoles" })
            {
                migrationBuilder.Sql($"ALTER TABLE con.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
                { "ContactPersons", "ContactAddresses", "ContactPersonRoles", "Contacts" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\";");
            }

            migrationBuilder.DropTable(name: "ContactAddresses", schema: "con");
            migrationBuilder.DropTable(name: "ContactPersons", schema: "con");
            migrationBuilder.DropTable(name: "ContactPersonRoles", schema: "con");
            migrationBuilder.DropTable(name: "Contacts", schema: "con");
        }
    }
}
