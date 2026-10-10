using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admission.Repository.Migrations
{
    /// <inheritdoc />
    public partial class Migration_01_InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "adm");

            migrationBuilder.CreateTable(
                name: "EditLog",
                schema: "adm",
                columns: table => new
                {
                    EditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RootEntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RootEntityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Changes = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_EditLog", x => x.EditLogId);
                });

            migrationBuilder.CreateTable(
                name: "Enquiries",
                schema: "adm",
                columns: table => new
                {
                    EnquiryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnquiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ChildName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    SeekingClassId = table.Column<long>(type: "bigint", nullable: false),
                    AcademicYearId = table.Column<long>(type: "bigint", nullable: false),
                    ParentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EnquirySource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EnquiryStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_Enquiries", x => x.EnquiryId);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "adm",
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
                name: "Applications",
                schema: "adm",
                columns: table => new
                {
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EnquiryId = table.Column<long>(type: "bigint", nullable: true),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ChildFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChildLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    ChildGender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SeekingClassId = table.Column<long>(type: "bigint", nullable: false),
                    AcademicYearId = table.Column<long>(type: "bigint", nullable: false),
                    GuardianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GuardianPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GuardianEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    GuardianRelationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GuardianContactId = table.Column<long>(type: "bigint", nullable: true),
                    ApplicationStage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssessmentScore = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AdmittedStudentId = table.Column<long>(type: "bigint", nullable: true),
                    AdmissionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ApplicationFee = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Applications", x => x.ApplicationId);
                    table.CheckConstraint("chk_application_admitted", "\"ApplicationStage\" <> 'Admitted' OR \"AdmittedStudentId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Applications_Enquiries_EnquiryId",
                        column: x => x.EnquiryId,
                        principalSchema: "adm",
                        principalTable: "Enquiries",
                        principalColumn: "EnquiryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationDocuments",
                schema: "adm",
                columns: table => new
                {
                    ApplicationDocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AttachmentKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ApplicationDocuments", x => x.ApplicationDocumentId);
                    table.ForeignKey(
                        name: "FK_ApplicationDocuments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "adm",
                        principalTable: "Applications",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_ApplicationId",
                schema: "adm",
                table: "ApplicationDocuments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_CustomerId_OrgId",
                schema: "adm",
                table: "ApplicationDocuments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CustomerId_OrgId",
                schema: "adm",
                table: "Applications",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CustomerId_OrgId_ApplicationNo",
                schema: "adm",
                table: "Applications",
                columns: new[] { "CustomerId", "OrgId", "ApplicationNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_EnquiryId",
                schema: "adm",
                table: "Applications",
                column: "EnquiryId",
                unique: true,
                filter: "\"EnquiryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_OrgId_ApplicationStage",
                schema: "adm",
                table: "Applications",
                columns: new[] { "OrgId", "ApplicationStage" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_CustomerId_OrgId",
                schema: "adm",
                table: "EditLog",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_ChangedAt",
                schema: "adm",
                table: "EditLog",
                columns: new[] { "OrgId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_EntityName_ChangedAt",
                schema: "adm",
                table: "EditLog",
                columns: new[] { "OrgId", "EntityName", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_RootEntityName_RootEntityKey_ChangedAt",
                schema: "adm",
                table: "EditLog",
                columns: new[] { "OrgId", "RootEntityName", "RootEntityKey", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_UserId_ChangedAt",
                schema: "adm",
                table: "EditLog",
                columns: new[] { "OrgId", "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_CustomerId_OrgId",
                schema: "adm",
                table: "Enquiries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_OrgId_EnquiryStatus_FollowUpDate",
                schema: "adm",
                table: "Enquiries",
                columns: new[] { "OrgId", "EnquiryStatus", "FollowUpDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "adm",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "adm",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "adm",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });
            // ---- Hand-written: what EF Core cannot generate (TK-185) ----------
            //
            // Row-level security on every tenant table: enabled, FORCEd, and one
            // policy on CustomerId and OrgId in TK-71's form, through
            // TenantRlsMigration. FORCE is what makes it bind, because the
            // application connects as the tables' owner. The edit log's policy and
            // its append-only triggers come from EditLogMigration (TK-118).
            //
            // Carry this block forward by hand in any future squash. The model
            // knows nothing of policies, triggers or functions, so a regenerated
            // migration drops them without a word, which is how RLS was lost on
            // 14 September 2026. A tenant table added later needs its own
            // TenantRlsMigration.Enable call; RlsAudit fails until it has one.
            foreach ((string schema, string[] tables) in RlsTables)
            {
                foreach (string table in tables)
                {
                    Shared.Kernel.Persistence.TenantRlsMigration.Enable(migrationBuilder, schema, table);
                }
            }

            Shared.Kernel.Audit.EditLogMigration.Up(migrationBuilder, "adm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Shared.Kernel.Audit.EditLogMigration.Down(migrationBuilder, "adm");

            migrationBuilder.DropTable(
                name: "ApplicationDocuments",
                schema: "adm");

            migrationBuilder.DropTable(
                name: "EditLog",
                schema: "adm");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "adm");

            migrationBuilder.DropTable(
                name: "Applications",
                schema: "adm");

            migrationBuilder.DropTable(
                name: "Enquiries",
                schema: "adm");
        }

        /// <summary>
        /// Every table guarded by the standard tenant policy, written out rather
        /// than derived at run time: a migration must do the same thing forever.
        /// Taken from the policies the old chains left in a migrated database.
        /// </summary>
        private static readonly (string Schema, string[] Tables)[] RlsTables =
        [
            ("adm",
            [
                "ApplicationDocuments",
                "Applications",
                "Enquiries",
                "ErrorLogs",
            ]),
        ];
    }
}
