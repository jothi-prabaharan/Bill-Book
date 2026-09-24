using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSlaPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                schema: "cus",
                columns: table => new
                {
                    SlaPolicyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResponseHours = table.Column<int>(type: "integer", nullable: false),
                    ResolutionHours = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SlaPolicies", x => x.SlaPolicyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_CustomerId_OrgId",
                schema: "cus",
                table: "SlaPolicies",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_OrgId_Priority",
                schema: "cus",
                table: "SlaPolicies",
                columns: new[] { "OrgId", "Priority" },
                unique: true);

            // Row-level security, as EnableRowLevelSecurity gives every other
            // cus table (TK-73; acc's is the template, TK-71). FORCE because the
            // application owns the table; USING only, so the same test guards
            // writes; NULLIF so no tenant sees nothing rather than throwing.
            migrationBuilder.Sql("""
                ALTER TABLE cus."SlaPolicies" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."SlaPolicies" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS slapolicies_tenant_isolation ON cus."SlaPolicies";
                CREATE POLICY slapolicies_tenant_isolation
                    ON cus."SlaPolicies"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP POLICY IF EXISTS slapolicies_tenant_isolation ON cus."SlaPolicies";""");

            migrationBuilder.DropTable(
                name: "SlaPolicies",
                schema: "cus");
        }
    }
}
