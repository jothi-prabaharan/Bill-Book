using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PortalGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalGrants",
                schema: "con",
                columns: table => new
                {
                    PortalGrantId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    App = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalGrants", x => x.PortalGrantId);
                    table.ForeignKey(
                        name: "FK_PortalGrants_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "con",
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalGrants_CodeHash",
                schema: "con",
                table: "PortalGrants",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalGrants_ContactId",
                schema: "con",
                table: "PortalGrants",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalGrants_CustomerId_OrgId",
                schema: "con",
                table: "PortalGrants",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalGrants_OrgId_ContactId",
                schema: "con",
                table: "PortalGrants",
                columns: new[] { "OrgId", "ContactId" });

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF. No special read policy: the session exchange
            // takes the customer and branch from the code and sets them before
            // it looks the grant up (TK-94).
            migrationBuilder.Sql("""
                ALTER TABLE con."PortalGrants" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE con."PortalGrants" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS portalgrants_tenant_isolation ON con."PortalGrants";
                CREATE POLICY portalgrants_tenant_isolation
                    ON con."PortalGrants"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalGrants",
                schema: "con");
        }
    }
}
