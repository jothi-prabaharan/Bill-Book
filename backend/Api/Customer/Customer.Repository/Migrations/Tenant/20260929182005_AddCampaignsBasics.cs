using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCampaignsBasics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audiences",
                schema: "cus",
                columns: table => new
                {
                    AudienceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rules = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Audiences", x => x.AudienceId);
                });

            migrationBuilder.CreateTable(
                name: "EmailConsents",
                schema: "cus",
                columns: table => new
                {
                    EmailConsentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_EmailConsents", x => x.EmailConsentId);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                schema: "cus",
                columns: table => new
                {
                    EmailTemplateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_EmailTemplates", x => x.EmailTemplateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_CustomerId_OrgId",
                schema: "cus",
                table: "Audiences",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_OrgId_IsActive",
                schema: "cus",
                table: "Audiences",
                columns: new[] { "OrgId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConsents_CustomerId_OrgId",
                schema: "cus",
                table: "EmailConsents",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConsents_OrgId_Email",
                schema: "cus",
                table: "EmailConsents",
                columns: new[] { "OrgId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_CustomerId_OrgId",
                schema: "cus",
                table: "EmailTemplates",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_OrgId_IsActive",
                schema: "cus",
                table: "EmailTemplates",
                columns: new[] { "OrgId", "IsActive" });

            migrationBuilder.Sql("""
                ALTER TABLE cus."EmailTemplates" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."EmailTemplates" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS emailtemplates_tenant_isolation ON cus."EmailTemplates";
                CREATE POLICY emailtemplates_tenant_isolation
                    ON cus."EmailTemplates"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);

                ALTER TABLE cus."Audiences" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."Audiences" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS audiences_tenant_isolation ON cus."Audiences";
                CREATE POLICY audiences_tenant_isolation
                    ON cus."Audiences"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);

                ALTER TABLE cus."EmailConsents" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."EmailConsents" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS emailconsents_tenant_isolation ON cus."EmailConsents";
                CREATE POLICY emailconsents_tenant_isolation
                    ON cus."EmailConsents"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS emailtemplates_tenant_isolation ON cus."EmailTemplates";
                DROP POLICY IF EXISTS audiences_tenant_isolation ON cus."Audiences";
                DROP POLICY IF EXISTS emailconsents_tenant_isolation ON cus."EmailConsents";
                """);

            migrationBuilder.DropTable(
                name: "Audiences",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "EmailConsents",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "EmailTemplates",
                schema: "cus");
        }
    }
}
