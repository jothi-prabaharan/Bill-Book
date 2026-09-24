using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ApprovalWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "apr");

            migrationBuilder.CreateTable(
                name: "ApprovalDelegates",
                schema: "apr",
                columns: table => new
                {
                    ApprovalDelegateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_ApprovalDelegates", x => x.ApprovalDelegateId);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflows",
                schema: "apr",
                columns: table => new
                {
                    ApprovalWorkflowId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    App = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: true),
                    GradeId = table.Column<long>(type: "bigint", nullable: true),
                    WorkLocationId = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_ApprovalWorkflows", x => x.ApprovalWorkflowId);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowLevels",
                schema: "apr",
                columns: table => new
                {
                    ApprovalWorkflowLevelId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalWorkflowId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApproverKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReportingDepth = table.Column<int>(type: "integer", nullable: true),
                    RelationshipTypeId = table.Column<long>(type: "bigint", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AboveAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false),
                    IsCommentRequired = table.Column<bool>(type: "boolean", nullable: false),
                    EscalateAfterDays = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_ApprovalWorkflowLevels", x => x.ApprovalWorkflowLevelId);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowLevels_ApprovalWorkflows_ApprovalWorkflowId",
                        column: x => x.ApprovalWorkflowId,
                        principalSchema: "apr",
                        principalTable: "ApprovalWorkflows",
                        principalColumn: "ApprovalWorkflowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegates_CustomerId_OrgId",
                schema: "apr",
                table: "ApprovalDelegates",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegates_UserId_FromDate_ToDate",
                schema: "apr",
                table: "ApprovalDelegates",
                columns: new[] { "UserId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowLevels_ApprovalWorkflowId_Sequence",
                schema: "apr",
                table: "ApprovalWorkflowLevels",
                columns: new[] { "ApprovalWorkflowId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowLevels_CustomerId_OrgId",
                schema: "apr",
                table: "ApprovalWorkflowLevels",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_CustomerId_OrgId",
                schema: "apr",
                table: "ApprovalWorkflows",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_OrgId_RequestKind_IsActive",
                schema: "apr",
                table: "ApprovalWorkflows",
                columns: new[] { "OrgId", "RequestKind", "IsActive" });
        
            // RLS on the three apr tables (TK-99), in TK-71's form: ENABLE,
            // FORCE, one policy on CustomerId and OrgId with NULLIF.
            foreach (string table in new[] { "ApprovalWorkflows", "ApprovalWorkflowLevels", "ApprovalDelegates" })
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE apr."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE apr."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON apr."{table}";
                    CREATE POLICY {policy}
                        ON apr."{table}"
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
            migrationBuilder.DropTable(
                name: "ApprovalDelegates",
                schema: "apr");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflowLevels",
                schema: "apr");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflows",
                schema: "apr");
        }
    }
}
