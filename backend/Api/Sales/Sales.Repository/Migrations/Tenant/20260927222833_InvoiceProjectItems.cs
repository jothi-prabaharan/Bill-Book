using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class InvoiceProjectItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceProjectItems",
                schema: "sal",
                columns: table => new
                {
                    InvoiceProjectItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_InvoiceProjectItems", x => x.InvoiceProjectItemId);
                    table.ForeignKey(
                        name: "FK_InvoiceProjectItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceProjectItems_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceProjectItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceProjectItems_InvoiceId",
                schema: "sal",
                table: "InvoiceProjectItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceProjectItems_OrgId_InvoiceId_Kind_ItemId",
                schema: "sal",
                table: "InvoiceProjectItems",
                columns: new[] { "OrgId", "InvoiceId", "Kind", "ItemId" },
                unique: true);

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-106).
            migrationBuilder.Sql("""
                ALTER TABLE sal."InvoiceProjectItems" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sal."InvoiceProjectItems" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS invoiceprojectitems_tenant_isolation ON sal."InvoiceProjectItems";
                CREATE POLICY invoiceprojectitems_tenant_isolation
                    ON sal."InvoiceProjectItems"
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
                name: "InvoiceProjectItems",
                schema: "sal");
        }
    }
}
