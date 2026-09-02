using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Customer.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCustomerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cus");

            migrationBuilder.CreateTable(
                name: "Leads",
                schema: "cus",
                columns: table => new
                {
                    LeadId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConvertedContactId = table.Column<long>(type: "bigint", nullable: true),
                    ConvertedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_Leads", x => x.LeadId);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "cus",
                columns: table => new
                {
                    TicketId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SlaDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_Tickets", x => x.TicketId);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                schema: "cus",
                columns: table => new
                {
                    TicketMessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_TicketMessages", x => x.TicketMessageId);
                    table.ForeignKey(
                        name: "FK_TicketMessages_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "cus",
                        principalTable: "Tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CustomerId_OrgId",
                schema: "cus",
                table: "Leads",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OrgId_Status",
                schema: "cus",
                table: "Leads",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_CustomerId_OrgId",
                schema: "cus",
                table: "TicketMessages",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_OrgId_TicketId",
                schema: "cus",
                table: "TicketMessages",
                columns: new[] { "OrgId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId",
                schema: "cus",
                table: "TicketMessages",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CustomerId_OrgId",
                schema: "cus",
                table: "Tickets",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrgId_ContactId",
                schema: "cus",
                table: "Tickets",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrgId_Status",
                schema: "cus",
                table: "Tickets",
                columns: new[] { "OrgId", "Status" });

            // Real FKs into con.Contacts: cus and con are schemas in the same
            // per-customer database, unlike the mst cross-database case, so the
            // reference is enforced rather than left as a plain unenforced id.
            // Raw SQL because the target entity belongs to another service's
            // Entity project and mapping it here would cross that boundary.
            migrationBuilder.Sql(@"
                ALTER TABLE cus.""Leads"" ADD CONSTRAINT ""FK_Leads_Contacts_ConvertedContactId"" FOREIGN KEY (""ConvertedContactId"") REFERENCES con.""Contacts"" (""ContactId"") ON DELETE RESTRICT;
                ALTER TABLE cus.""Tickets"" ADD CONSTRAINT ""FK_Tickets_Contacts_ContactId"" FOREIGN KEY (""ContactId"") REFERENCES con.""Contacts"" (""ContactId"") ON DELETE RESTRICT;
            ");

            // Row-level security: the database-level half of tenant isolation,
            // independent of the EF query filter. No LINQ equivalent exists.
            foreach (string table in new[] { "Leads", "Tickets", "TicketMessages" })
            {
                migrationBuilder.Sql($"ALTER TABLE cus.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE cus.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON cus.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Leads",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "TicketMessages",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "cus");
        }
    }
}
