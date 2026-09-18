using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Printing.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialPrintingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "prt");

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "prt",
                columns: table => new
                {
                    ErrorLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ErrorReference = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    SqlState = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    JobReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExceptionType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Hint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Where = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Routine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConstraintName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SchemaName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ColumnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StackTrace = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    InnerExceptions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FollowUpStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FollowUpNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ErrorLogs", x => x.ErrorLogId);
                });

            migrationBuilder.CreateTable(
                name: "PrintTemplates",
                schema: "prt",
                columns: table => new
                {
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentTypeCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    SeedVersion = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PrintTemplates", x => x.PrintTemplateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "prt",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "prt",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "prt",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_CustomerId_OrgId",
                schema: "prt",
                table: "PrintTemplates",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_Default",
                schema: "prt",
                table: "PrintTemplates",
                columns: new[] { "CustomerId", "OrgId", "DocumentTypeCode" },
                unique: true,
                filter: "\"IsDefault\" AND \"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_Name",
                schema: "prt",
                table: "PrintTemplates",
                columns: new[] { "CustomerId", "OrgId", "DocumentTypeCode", "TemplateName" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_OrgId_DocumentTypeCode_IsActive",
                schema: "prt",
                table: "PrintTemplates",
                columns: new[] { "OrgId", "DocumentTypeCode", "IsActive" });

            // Row-level security, written by hand because EF Core generates
            // none of it. The EF query filter on TenantDbContext and this policy
            // say the same thing in two places on purpose: neither is trusted
            // alone, and a query that escapes the filter — IgnoreQueryFilters, a
            // raw command, a mapping that was forgotten — still meets this.
            //
            // FORCE is the clause that makes it real. Without it RLS does not
            // apply to the table's owner, and the application connects as the
            // role that owns these tables, so every policy here would be inert
            // and the filter would be the only guard left.
            //
            // FOR ALL with USING and no WITH CHECK is deliberate: Postgres
            // reuses the USING expression as the check for INSERT and UPDATE, so
            // a row cannot be written into another branch either.
            //
            // The ids come from set_config(..., true) — transaction-local, set
            // per request by RlsConnectionInterceptor. Connection-level would
            // leak tenant context across pooled connections.
            foreach (string table in new[] { "PrintTemplates", "ErrorLogs" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE prt."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE prt."{table}" FORCE ROW LEVEL SECURITY;
                    CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation
                        ON prt."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = current_setting('app.current_customer_id', true)::uuid
                            AND "OrgId" = current_setting('app.current_org_id', true)::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "prt");

            migrationBuilder.DropTable(
                name: "PrintTemplates",
                schema: "prt");
        }
    }
}
