using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Contacts.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddContactDocumentsAndBanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_OrgId",
                schema: "con",
                table: "ContactBankDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_ContactId",
                schema: "con",
                table: "ContactBankDetails",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_OrgId_ContactId",
                schema: "con",
                table: "ContactBankDetails",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactBankDetails_Default",
                schema: "con",
                table: "ContactBankDetails",
                columns: new[] { "OrgId", "ContactId" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_OrgId",
                schema: "con",
                table: "ContactLicences",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_ContactId",
                schema: "con",
                table: "ContactLicences",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_OrgId_ContactId",
                schema: "con",
                table: "ContactLicences",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactLicences_Expiry",
                schema: "con",
                table: "ContactLicences",
                columns: new[] { "OrgId", "ExpiresOn" },
                filter: "\"ExpiresOn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_OrgId",
                schema: "con",
                table: "ContactAttachments",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAttachments_ContactId",
                schema: "con",
                table: "ContactAttachments",
                column: "ContactId");

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

            foreach (string table in new[]
                { "ContactBankDetails", "ContactLicences", "ContactAttachments" })
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
                { "ContactAttachments", "ContactLicences", "ContactBankDetails" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\";");
            }

            migrationBuilder.DropTable(name: "ContactAttachments", schema: "con");
            migrationBuilder.DropTable(name: "ContactLicences", schema: "con");
            migrationBuilder.DropTable(name: "ContactBankDetails", schema: "con");
        }
    }
}
