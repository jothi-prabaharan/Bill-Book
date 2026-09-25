using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Claims.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialClaimsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clm");

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflows",
                schema: "clm",
                columns: table => new
                {
                    ApprovalWorkflowId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                name: "ClaimCategories",
                schema: "clm",
                columns: table => new
                {
                    ClaimCategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsReceiptRequired = table.Column<bool>(type: "boolean", nullable: false),
                    LedgerAccountId = table.Column<long>(type: "bigint", nullable: true),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ClaimCategories", x => x.ClaimCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "clm",
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
                name: "ExpenseClaims",
                schema: "clm",
                columns: table => new
                {
                    ExpenseClaimId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ClaimStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayoutMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayrollRunId = table.Column<long>(type: "bigint", nullable: true),
                    SpendMoneyId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentStepLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentApproverEmployeeId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ExpenseClaims", x => x.ExpenseClaimId);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowLevels",
                schema: "clm",
                columns: table => new
                {
                    ApprovalWorkflowLevelId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalWorkflowId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApproverKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReportingDepth = table.Column<int>(type: "integer", nullable: true),
                    SpecificRoleId = table.Column<int>(type: "integer", nullable: true),
                    SpecificEmployeeId = table.Column<long>(type: "bigint", nullable: true),
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
                        principalSchema: "clm",
                        principalTable: "ApprovalWorkflows",
                        principalColumn: "ApprovalWorkflowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimLimits",
                schema: "clm",
                columns: table => new
                {
                    ClaimLimitId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    GradeId = table.Column<long>(type: "bigint", nullable: true),
                    LimitPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_ClaimLimits", x => x.ClaimLimitId);
                    table.ForeignKey(
                        name: "FK_ClaimLimits_ClaimCategories_ClaimCategoryId",
                        column: x => x.ClaimCategoryId,
                        principalSchema: "clm",
                        principalTable: "ClaimCategories",
                        principalColumn: "ClaimCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "clm",
                columns: table => new
                {
                    ApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpenseClaimId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StepStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApproverEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    ActedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ApprovalSteps", x => x.ApprovalStepId);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalSchema: "clm",
                        principalTable: "ExpenseClaims",
                        principalColumn: "ExpenseClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseClaimLines",
                schema: "clm",
                columns: table => new
                {
                    ExpenseClaimLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpenseClaimId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceiptAttachmentKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ExpenseClaimLines", x => x.ExpenseClaimLineId);
                    table.ForeignKey(
                        name: "FK_ExpenseClaimLines_ClaimCategories_ClaimCategoryId",
                        column: x => x.ClaimCategoryId,
                        principalSchema: "clm",
                        principalTable: "ClaimCategories",
                        principalColumn: "ClaimCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseClaimLines_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalSchema: "clm",
                        principalTable: "ExpenseClaims",
                        principalColumn: "ExpenseClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "clm",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ExpenseClaimId",
                schema: "clm",
                table: "ApprovalSteps",
                column: "ExpenseClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowLevels_ApprovalWorkflowId",
                schema: "clm",
                table: "ApprovalWorkflowLevels",
                column: "ApprovalWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowLevels_CustomerId_OrgId",
                schema: "clm",
                table: "ApprovalWorkflowLevels",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_CustomerId_OrgId",
                schema: "clm",
                table: "ApprovalWorkflows",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimCategories_CustomerId_OrgId",
                schema: "clm",
                table: "ClaimCategories",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimCategories_CustomerId_OrgId_Code",
                schema: "clm",
                table: "ClaimCategories",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimLimits_ClaimCategoryId",
                schema: "clm",
                table: "ClaimLimits",
                column: "ClaimCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimLimits_CustomerId_OrgId",
                schema: "clm",
                table: "ClaimLimits",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "clm",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "clm",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "clm",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaimLines_ClaimCategoryId",
                schema: "clm",
                table: "ExpenseClaimLines",
                column: "ClaimCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaimLines_CustomerId_OrgId",
                schema: "clm",
                table: "ExpenseClaimLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaimLines_ExpenseClaimId",
                schema: "clm",
                table: "ExpenseClaimLines",
                column: "ExpenseClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_CustomerId_OrgId",
                schema: "clm",
                table: "ExpenseClaims",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_CustomerId_OrgId_ClaimNo",
                schema: "clm",
                table: "ExpenseClaims",
                columns: new[] { "CustomerId", "OrgId", "ClaimNo" },
                unique: true);

            string[] tables =
            {
                "ApprovalSteps",
                "ApprovalWorkflowLevels",
                "ApprovalWorkflows",
                "ClaimCategories",
                "ClaimLimits",
                "ErrorLogs",
                "ExpenseClaims",
                "ExpenseClaimLines"
            };

            foreach (string table in tables)
            {
                migrationBuilder.Sql($"ALTER TABLE clm.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE clm.\"{table}\" FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON clm."{table}"
                        FOR ALL
                        USING ("CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                           AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            string[] tables =
            {
                "ApprovalSteps",
                "ApprovalWorkflowLevels",
                "ApprovalWorkflows",
                "ClaimCategories",
                "ClaimLimits",
                "ErrorLogs",
                "ExpenseClaims",
                "ExpenseClaimLines"
            };

            foreach (string table in tables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON clm.\"{table}\";");
            }

            migrationBuilder.DropTable(
                name: "ApprovalSteps",
                schema: "clm");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflowLevels",
                schema: "clm");

            migrationBuilder.DropTable(
                name: "ClaimLimits",
                schema: "clm");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "clm");

            migrationBuilder.DropTable(
                name: "ExpenseClaimLines",
                schema: "clm");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflows",
                schema: "clm");

            migrationBuilder.DropTable(
                name: "ClaimCategories",
                schema: "clm");

            migrationBuilder.DropTable(
                name: "ExpenseClaims",
                schema: "clm");
        }
    }
}
