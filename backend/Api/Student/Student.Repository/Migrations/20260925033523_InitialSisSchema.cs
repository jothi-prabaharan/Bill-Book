using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Student.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialSisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sis");

            migrationBuilder.CreateTable(
                name: "AcademicYears",
                schema: "sis",
                columns: table => new
                {
                    AcademicYearId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AcademicYears", x => x.AcademicYearId);
                    table.CheckConstraint("chk_academic_year_dates", "\"EndDate\" > \"StartDate\"");
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "sis",
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
                name: "SchoolClasses",
                schema: "sis",
                columns: table => new
                {
                    SchoolClassId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SchoolClasses", x => x.SchoolClassId);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                schema: "sis",
                columns: table => new
                {
                    StudentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AdmissionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StudentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LeavingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BloodGroup = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    NationalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PhotoAttachmentKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceApplicationId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Students", x => x.StudentId);
                    table.CheckConstraint("chk_student_leaving", "\"StudentStatus\" = 'Active' OR \"LeavingDate\" IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                schema: "sis",
                columns: table => new
                {
                    SubjectId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_Subjects", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "Exams",
                schema: "sis",
                columns: table => new
                {
                    ExamId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExamStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_Exams", x => x.ExamId);
                    table.CheckConstraint("chk_exam_dates", "\"EndDate\" >= \"StartDate\"");
                    table.ForeignKey(
                        name: "FK_Exams_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "sis",
                        principalTable: "AcademicYears",
                        principalColumn: "AcademicYearId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                schema: "sis",
                columns: table => new
                {
                    SectionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<long>(type: "bigint", nullable: false),
                    SchoolClassId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    ClassTeacherEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    RoomSpaceId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Sections", x => x.SectionId);
                    table.ForeignKey(
                        name: "FK_Sections_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "sis",
                        principalTable: "AcademicYears",
                        principalColumn: "AcademicYearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sections_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalSchema: "sis",
                        principalTable: "SchoolClasses",
                        principalColumn: "SchoolClassId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentGuardians",
                schema: "sis",
                columns: table => new
                {
                    StudentGuardianId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    HasPortalAccess = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_StudentGuardians", x => x.StudentGuardianId);
                    table.ForeignKey(
                        name: "FK_StudentGuardians_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "sis",
                        principalTable: "Students",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamSubjects",
                schema: "sis",
                columns: table => new
                {
                    ExamSubjectId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    SchoolClassId = table.Column<long>(type: "bigint", nullable: false),
                    MaxMarks = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PassMarks = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_ExamSubjects", x => x.ExamSubjectId);
                    table.CheckConstraint("chk_exam_subject_marks", "\"MaxMarks\" > 0 AND \"PassMarks\" >= 0 AND \"PassMarks\" <= \"MaxMarks\"");
                    table.ForeignKey(
                        name: "FK_ExamSubjects_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "sis",
                        principalTable: "Exams",
                        principalColumn: "ExamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamSubjects_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalSchema: "sis",
                        principalTable: "SchoolClasses",
                        principalColumn: "SchoolClassId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "sis",
                        principalTable: "Subjects",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrolments",
                schema: "sis",
                columns: table => new
                {
                    EnrolmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    AcademicYearId = table.Column<long>(type: "bigint", nullable: false),
                    SectionId = table.Column<long>(type: "bigint", nullable: false),
                    RollNo = table.Column<int>(type: "integer", nullable: true),
                    EnrolmentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_Enrolments", x => x.EnrolmentId);
                    table.ForeignKey(
                        name: "FK_Enrolments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "sis",
                        principalTable: "AcademicYears",
                        principalColumn: "AcademicYearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrolments_Sections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "sis",
                        principalTable: "Sections",
                        principalColumn: "SectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrolments_Students_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "sis",
                        principalTable: "Students",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamMarks",
                schema: "sis",
                columns: table => new
                {
                    ExamMarkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamSubjectId = table.Column<long>(type: "bigint", nullable: false),
                    EnrolmentId = table.Column<long>(type: "bigint", nullable: false),
                    Marks = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsAbsent = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ExamMarks", x => x.ExamMarkId);
                    table.CheckConstraint("chk_exam_mark", "(\"IsAbsent\" = true AND \"Marks\" IS NULL) OR (\"IsAbsent\" = false AND \"Marks\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ExamMarks_Enrolments_EnrolmentId",
                        column: x => x.EnrolmentId,
                        principalSchema: "sis",
                        principalTable: "Enrolments",
                        principalColumn: "EnrolmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamMarks_ExamSubjects_ExamSubjectId",
                        column: x => x.ExamSubjectId,
                        principalSchema: "sis",
                        principalTable: "ExamSubjects",
                        principalColumn: "ExamSubjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_CustomerId_OrgId_Code",
                schema: "sis",
                table: "AcademicYears",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_OneCurrent",
                schema: "sis",
                table: "AcademicYears",
                columns: new[] { "CustomerId", "OrgId" },
                unique: true,
                filter: "\"IsCurrent\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_AcademicYearId",
                schema: "sis",
                table: "Enrolments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_CustomerId_OrgId",
                schema: "sis",
                table: "Enrolments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_SectionId_RollNo",
                schema: "sis",
                table: "Enrolments",
                columns: new[] { "SectionId", "RollNo" },
                unique: true,
                filter: "\"RollNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_StudentId_AcademicYearId",
                schema: "sis",
                table: "Enrolments",
                columns: new[] { "StudentId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "sis",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "sis",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "sis",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamMarks_CustomerId_OrgId",
                schema: "sis",
                table: "ExamMarks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamMarks_EnrolmentId",
                schema: "sis",
                table: "ExamMarks",
                column: "EnrolmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamMarks_ExamSubjectId_EnrolmentId",
                schema: "sis",
                table: "ExamMarks",
                columns: new[] { "ExamSubjectId", "EnrolmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamSubjects_CustomerId_OrgId",
                schema: "sis",
                table: "ExamSubjects",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamSubjects_ExamId_SubjectId_SchoolClassId",
                schema: "sis",
                table: "ExamSubjects",
                columns: new[] { "ExamId", "SubjectId", "SchoolClassId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamSubjects_SchoolClassId",
                schema: "sis",
                table: "ExamSubjects",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSubjects_SubjectId",
                schema: "sis",
                table: "ExamSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_AcademicYearId",
                schema: "sis",
                table: "Exams",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CustomerId_OrgId",
                schema: "sis",
                table: "Exams",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_CustomerId_OrgId",
                schema: "sis",
                table: "SchoolClasses",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_CustomerId_OrgId_Code",
                schema: "sis",
                table: "SchoolClasses",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_AcademicYearId_SchoolClassId_Name",
                schema: "sis",
                table: "Sections",
                columns: new[] { "AcademicYearId", "SchoolClassId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_CustomerId_OrgId",
                schema: "sis",
                table: "Sections",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sections_SchoolClassId",
                schema: "sis",
                table: "Sections",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_CustomerId_OrgId",
                schema: "sis",
                table: "StudentGuardians",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_OnePrimary",
                schema: "sis",
                table: "StudentGuardians",
                column: "StudentId",
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_OrgId_ContactId",
                schema: "sis",
                table: "StudentGuardians",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardians_StudentId_ContactId",
                schema: "sis",
                table: "StudentGuardians",
                columns: new[] { "StudentId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_CustomerId_OrgId",
                schema: "sis",
                table: "Students",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_CustomerId_OrgId_AdmissionNo",
                schema: "sis",
                table: "Students",
                columns: new[] { "CustomerId", "OrgId", "AdmissionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_CustomerId_OrgId_SourceApplicationId",
                schema: "sis",
                table: "Students",
                columns: new[] { "CustomerId", "OrgId", "SourceApplicationId" },
                unique: true,
                filter: "\"SourceApplicationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_OrgId_StudentStatus",
                schema: "sis",
                table: "Students",
                columns: new[] { "OrgId", "StudentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CustomerId_OrgId",
                schema: "sis",
                table: "Subjects",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CustomerId_OrgId_Code",
                schema: "sis",
                table: "Subjects",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            // Row-level security on every sis table (S1, TK-61), in the same
            // migration that creates them, in TK-71's form: ENABLE, FORCE, one
            // policy on CustomerId and OrgId, and NULLIF so a request with no
            // tenant sees nothing rather than failing on ''::uuid. No WITH
            // CHECK, so USING is also the write check.
            foreach (string table in RlsTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE sis."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE sis."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON sis."{table}";
                    CREATE POLICY {policy}
                        ON sis."{table}"
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
                name: "ErrorLogs",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "ExamMarks",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "StudentGuardians",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "Enrolments",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "ExamSubjects",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "Sections",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "Students",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "Exams",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "Subjects",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "SchoolClasses",
                schema: "sis");

            migrationBuilder.DropTable(
                name: "AcademicYears",
                schema: "sis");
        }

        private static readonly string[] RlsTables =
        [
            "AcademicYears",
            "ErrorLogs",
            "SchoolClasses",
            "Students",
            "Subjects",
            "Exams",
            "Sections",
            "StudentGuardians",
            "ExamSubjects",
            "Enrolments",
            "ExamMarks",
        ];
    }
}
