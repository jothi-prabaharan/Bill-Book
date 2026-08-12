using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Contacts
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
                    SubLedgerProvisionedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                name: "ContactAttachments",
                schema: "con",
                columns: table => new
                {
                    ContactAttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ContactAttachments", x => x.ContactAttachmentId);
                    table.CheckConstraint("chk_attachment_size", "\"FileSizeBytes\" > 0");
                    table.ForeignKey(
                        name: "FK_ContactAttachments_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "con",
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactBankDetails",
                schema: "con",
                columns: table => new
                {
                    ContactBankDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Ifsc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccountKind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UpiId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_ContactBankDetails", x => x.ContactBankDetailId);
                    table.ForeignKey(
                        name: "FK_ContactBankDetails_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "con",
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactLicences",
                schema: "con",
                columns: table => new
                {
                    ContactLicenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    LicenceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LicenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IssuingAuthority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ContactLicences", x => x.ContactLicenceId);
                    table.CheckConstraint("chk_licence_dates", "\"IssuedOn\" IS NULL OR \"ExpiresOn\" IS NULL OR \"ExpiresOn\" >= \"IssuedOn\"");
                    table.ForeignKey(
                        name: "FK_ContactLicences_Contacts_ContactId",
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
                        name: "FK_ContactPersons_ContactPersonRoles_ContactPersonRoleId",
                        column: x => x.ContactPersonRoleId,
                        principalSchema: "con",
                        principalTable: "ContactPersonRoles",
                        principalColumn: "ContactPersonRoleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactPersons_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "con",
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_ContactId",
                schema: "con",
                table: "ContactAddresses",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_Default",
                schema: "con",
                table: "ContactAddresses",
                columns: new[] { "OrgId", "ContactId", "AddressType" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_OrgId",
                schema: "con",
                table: "ContactAddresses",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_ContactId",
                schema: "con",
                table: "ContactAttachments",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_OrgId",
                schema: "con",
                table: "ContactAttachments",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_OrgId_ContactId",
                schema: "con",
                table: "ContactAttachments",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_Storage",
                schema: "con",
                table: "ContactAttachments",
                columns: new[] { "OrgId", "StoragePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_ContactId",
                schema: "con",
                table: "ContactBankDetails",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_Default",
                schema: "con",
                table: "ContactBankDetails",
                columns: new[] { "OrgId", "ContactId" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_OrgId",
                schema: "con",
                table: "ContactBankDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_ContactId",
                schema: "con",
                table: "ContactLicences",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_Expiry",
                schema: "con",
                table: "ContactLicences",
                columns: new[] { "OrgId", "ExpiresOn" },
                filter: "\"ExpiresOn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_OrgId",
                schema: "con",
                table: "ContactLicences",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_OrgId_ContactId",
                schema: "con",
                table: "ContactLicences",
                columns: new[] { "OrgId", "ContactId" });

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
                name: "IX_ContactPersons_Default",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "OrgId", "ContactId" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_Email",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "OrgId", "Email" },
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_Mobile",
                schema: "con",
                table: "ContactPersons",
                columns: new[] { "OrgId", "MobileNumber" },
                filter: "\"MobileNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_OrgId",
                schema: "con",
                table: "ContactPersons",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Customer",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsCustomer\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Gstin",
                schema: "con",
                table: "Contacts",
                columns: new[] { "OrgId", "Gstin" },
                unique: true,
                filter: "\"Gstin\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_JobWorker",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsJobWorker\" = true");

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
                name: "IX_Contacts_Prescriber",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsPrescriber\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_SubLedgerPending",
                schema: "con",
                table: "Contacts",
                columns: new[] { "OrgId", "ContactId" },
                filter: "\"SubLedgerProvisionedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Vendor",
                schema: "con",
                table: "Contacts",
                column: "OrgId",
                filter: "\"IsVendor\" = true");

            // ---- Row-level security, which EF Core does not generate. ----
            //
            // Every con table is per-branch. The EF query filter is the first
            // line of defence; this is the one that holds if a query ever runs
            // without it, and it is one of the raw-SQL exceptions CLAUDE.md
            // allows. app.current_org_id is set per transaction — never on the
            // connection, which is pooled and would leak the last request's
            // branch into the next one.
            foreach (string table in new[]
            {
                "Contacts",
                "ContactAddresses",
                "ContactPersons",
                "ContactPersonRoles",
                "ContactBankDetails",
                "ContactLicences",
                "ContactAttachments",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE con.\"{table}\" ENABLE ROW LEVEL SECURITY;");

                // Dropped first so the migration is safe to re-run against a
                // database where it was applied by hand.
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactAddresses",
                schema: "con");

            migrationBuilder.DropTable(
                name: "ContactAttachments",
                schema: "con");

            migrationBuilder.DropTable(
                name: "ContactBankDetails",
                schema: "con");

            migrationBuilder.DropTable(
                name: "ContactLicences",
                schema: "con");

            migrationBuilder.DropTable(
                name: "ContactPersons",
                schema: "con");

            migrationBuilder.DropTable(
                name: "ContactPersonRoles",
                schema: "con");

            migrationBuilder.DropTable(
                name: "Contacts",
                schema: "con");
        }
    }
}
