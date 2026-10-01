using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddServicePlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServicePlans",
                schema: "cus",
                columns: table => new
                {
                    ServicePlanId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    AssetId = table.Column<long>(type: "bigint", nullable: true),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeadDays = table.Column<int>(type: "integer", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ServicePlans", x => x.ServicePlanId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlans_CustomerId_OrgId",
                schema: "cus",
                table: "ServicePlans",
                columns: new[] { "CustomerId", "OrgId" });
                    string[] upTables = { "ServicePlans" };
            foreach (string table in upTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";
                migrationBuilder.Sql($"""
                    ALTER TABLE cus."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE cus."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON cus."{table}";
                    CREATE POLICY {policy}
                        ON cus."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            string[] downTables = { "ServicePlans" };
            foreach (string table in downTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {policy} ON cus."{table}";
                    ALTER TABLE cus."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE cus."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
            migrationBuilder.DropTable(
                name: "ServicePlans",
                schema: "cus");
        }
    }
}

