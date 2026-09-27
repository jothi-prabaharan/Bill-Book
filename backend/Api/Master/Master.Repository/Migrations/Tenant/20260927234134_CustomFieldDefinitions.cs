using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class CustomFieldDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cfd");

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                schema: "cfd",
                columns: table => new
                {
                    CustomFieldDefinitionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MaxLength = table.Column<int>(type: "integer", nullable: true),
                    ShowInList = table.Column<bool>(type: "boolean", nullable: false),
                    ShowOnPrint = table.Column<bool>(type: "boolean", nullable: false),
                    IsSearchable = table.Column<bool>(type: "boolean", nullable: false),
                    CarryForward = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Options = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions", x => x.CustomFieldDefinitionId);
                    table.CheckConstraint("chk_customfields_key", "\"Key\" ~ '^[a-z][a-z0-9_]*$'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_CustomerId_OrgId",
                schema: "cfd",
                table: "CustomFieldDefinitions",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_OrgId_EntityKind_IsActive_DisplayOrd~",
                schema: "cfd",
                table: "CustomFieldDefinitions",
                columns: new[] { "OrgId", "EntityKind", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_OrgId_EntityKind_Key",
                schema: "cfd",
                table: "CustomFieldDefinitions",
                columns: new[] { "OrgId", "EntityKind", "Key" },
                unique: true);

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-110).
            migrationBuilder.Sql("""
                ALTER TABLE cfd."CustomFieldDefinitions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cfd."CustomFieldDefinitions" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS customfielddefinitions_tenant_isolation ON cfd."CustomFieldDefinitions";
                CREATE POLICY customfielddefinitions_tenant_isolation
                    ON cfd."CustomFieldDefinitions"
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
                name: "CustomFieldDefinitions",
                schema: "cfd");
        }
    }
}
