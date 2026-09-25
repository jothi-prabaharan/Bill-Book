using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TimeLeave.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialTimeLeaveSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tla");

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "tla",
                columns: table => new
                {
                    ApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApproverEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    StepStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "BiometricDeviceUsers",
                schema: "tla",
                columns: table => new
                {
                    BiometricDeviceUserId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeviceUserId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_BiometricDeviceUsers", x => x.BiometricDeviceUserId);
                });

            migrationBuilder.CreateTable(
                name: "CompOffCredits",
                schema: "tla",
                columns: table => new
                {
                    CompOffCreditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EarnedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AvailedDays = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_CompOffCredits", x => x.CompOffCreditId);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "tla",
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
                name: "HolidayLists",
                schema: "tla",
                columns: table => new
                {
                    HolidayListId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkLocationId = table.Column<long>(type: "bigint", nullable: false),
                    CalendarYear = table.Column<int>(type: "integer", nullable: false),
                    MaxOptionalPerYear = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_HolidayLists", x => x.HolidayListId);
                });

            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                schema: "tla",
                columns: table => new
                {
                    LeaveTypeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    IsHalfDayAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    IsAttachmentRequiredAboveDays = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_LeaveTypes", x => x.LeaveTypeId);
                });

            migrationBuilder.CreateTable(
                name: "OvertimeRequests",
                schema: "tla",
                columns: table => new
                {
                    OvertimeRequestId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    OvertimeRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_OvertimeRequests", x => x.OvertimeRequestId);
                });

            migrationBuilder.CreateTable(
                name: "Punches",
                schema: "tla",
                columns: table => new
                {
                    PunchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PunchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PunchSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeviceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    IsInsideFence = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_Punches", x => x.PunchId);
                });

            migrationBuilder.CreateTable(
                name: "RegularisationRequests",
                schema: "tla",
                columns: table => new
                {
                    RegularisationRequestId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedIn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedOut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_RegularisationRequests", x => x.RegularisationRequestId);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                schema: "tla",
                columns: table => new
                {
                    ShiftId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    GraceInMinutes = table.Column<int>(type: "integer", nullable: false),
                    GraceOutMinutes = table.Column<int>(type: "integer", nullable: false),
                    HalfDayBelowMinutes = table.Column<int>(type: "integer", nullable: false),
                    AbsentBelowMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsNightShift = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Shifts", x => x.ShiftId);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyOffPolicies",
                schema: "tla",
                columns: table => new
                {
                    WeeklyOffPolicyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MondayRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TuesdayRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WednesdayRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ThursdayRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FridayRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SaturdayRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SundayRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AlternateWeeks = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_WeeklyOffPolicies", x => x.WeeklyOffPolicyId);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "tla",
                columns: table => new
                {
                    HolidayId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HolidayListId = table.Column<long>(type: "bigint", nullable: false),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Holidays", x => x.HolidayId);
                    table.ForeignKey(
                        name: "FK_Holidays_HolidayLists_HolidayListId",
                        column: x => x.HolidayListId,
                        principalSchema: "tla",
                        principalTable: "HolidayLists",
                        principalColumn: "HolidayListId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaveApplications",
                schema: "tla",
                columns: table => new
                {
                    LeaveApplicationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    LeaveTypeId = table.Column<long>(type: "bigint", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromHalf = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToHalf = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Days = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AttachmentKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LeaveStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_LeaveApplications", x => x.LeaveApplicationId);
                    table.ForeignKey(
                        name: "FK_LeaveApplications_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalSchema: "tla",
                        principalTable: "LeaveTypes",
                        principalColumn: "LeaveTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                schema: "tla",
                columns: table => new
                {
                    LeaveBalanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    LeaveTypeId = table.Column<long>(type: "bigint", nullable: false),
                    LeaveYear = table.Column<int>(type: "integer", nullable: false),
                    Opening = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Accrued = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Taken = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Encashed = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Lapsed = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Adjusted = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_LeaveBalances", x => x.LeaveBalanceId);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalSchema: "tla",
                        principalTable: "LeaveTypes",
                        principalColumn: "LeaveTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveEncashments",
                schema: "tla",
                columns: table => new
                {
                    LeaveEncashmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    LeaveTypeId = table.Column<long>(type: "bigint", nullable: false),
                    LeaveYear = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PayrollRunId = table.Column<long>(type: "bigint", nullable: true),
                    EncashmentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_LeaveEncashments", x => x.LeaveEncashmentId);
                    table.ForeignKey(
                        name: "FK_LeaveEncashments_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalSchema: "tla",
                        principalTable: "LeaveTypes",
                        principalColumn: "LeaveTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeavePolicies",
                schema: "tla",
                columns: table => new
                {
                    LeavePolicyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeaveTypeId = table.Column<long>(type: "bigint", nullable: false),
                    GradeId = table.Column<long>(type: "bigint", nullable: true),
                    WorkLocationId = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    AnnualQuota = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AccrualKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsProratedOnJoining = table.Column<bool>(type: "boolean", nullable: false),
                    CarryForwardKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxCarryForward = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxEncashPerYear = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MinDaysPerApplication = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxDaysPerApplication = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    NoticeDays = table.Column<int>(type: "integer", nullable: false),
                    IsSandwichRule = table.Column<bool>(type: "boolean", nullable: false),
                    CanApplyInProbation = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LeavePolicies", x => x.LeavePolicyId);
                    table.ForeignKey(
                        name: "FK_LeavePolicies_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalSchema: "tla",
                        principalTable: "LeaveTypes",
                        principalColumn: "LeaveTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyAttendances",
                schema: "tla",
                columns: table => new
                {
                    DailyAttendanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftId = table.Column<long>(type: "bigint", nullable: true),
                    FirstIn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastOut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WorkedMinutes = table.Column<int>(type: "integer", nullable: false),
                    LateMinutes = table.Column<int>(type: "integer", nullable: false),
                    EarlyOutMinutes = table.Column<int>(type: "integer", nullable: false),
                    OvertimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    AttendanceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttendanceSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_DailyAttendances", x => x.DailyAttendanceId);
                    table.ForeignKey(
                        name: "FK_DailyAttendances_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "tla",
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShiftRosters",
                schema: "tla",
                columns: table => new
                {
                    ShiftRosterId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftId = table.Column<long>(type: "bigint", nullable: false),
                    WeeklyOffPolicyId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_ShiftRosters", x => x.ShiftRosterId);
                    table.ForeignKey(
                        name: "FK_ShiftRosters_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "tla",
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftRosters_WeeklyOffPolicies_WeeklyOffPolicyId",
                        column: x => x.WeeklyOffPolicyId,
                        principalSchema: "tla",
                        principalTable: "WeeklyOffPolicies",
                        principalColumn: "WeeklyOffPolicyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "tla",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId_RequestKind_RequestId_Sequen~",
                schema: "tla",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId", "RequestKind", "RequestId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricDeviceUsers_CustomerId_OrgId",
                schema: "tla",
                table: "BiometricDeviceUsers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricDeviceUsers_CustomerId_OrgId_DeviceCode_DeviceUser~",
                schema: "tla",
                table: "BiometricDeviceUsers",
                columns: new[] { "CustomerId", "OrgId", "DeviceCode", "DeviceUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompOffCredits_CustomerId_OrgId",
                schema: "tla",
                table: "CompOffCredits",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendances_CustomerId_OrgId",
                schema: "tla",
                table: "DailyAttendances",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendances_CustomerId_OrgId_EmployeeId_AttendanceDate",
                schema: "tla",
                table: "DailyAttendances",
                columns: new[] { "CustomerId", "OrgId", "EmployeeId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendances_ShiftId",
                schema: "tla",
                table: "DailyAttendances",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "tla",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "tla",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "tla",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HolidayLists_CustomerId_OrgId",
                schema: "tla",
                table: "HolidayLists",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_CustomerId_OrgId",
                schema: "tla",
                table: "Holidays",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_CustomerId_OrgId_HolidayListId_HolidayDate",
                schema: "tla",
                table: "Holidays",
                columns: new[] { "CustomerId", "OrgId", "HolidayListId", "HolidayDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_HolidayListId",
                schema: "tla",
                table: "Holidays",
                column: "HolidayListId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_CustomerId_OrgId",
                schema: "tla",
                table: "LeaveApplications",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_LeaveTypeId",
                schema: "tla",
                table: "LeaveApplications",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_CustomerId_OrgId",
                schema: "tla",
                table: "LeaveBalances",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_CustomerId_OrgId_EmployeeId_LeaveTypeId_Leave~",
                schema: "tla",
                table: "LeaveBalances",
                columns: new[] { "CustomerId", "OrgId", "EmployeeId", "LeaveTypeId", "LeaveYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_LeaveTypeId",
                schema: "tla",
                table: "LeaveBalances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveEncashments_CustomerId_OrgId",
                schema: "tla",
                table: "LeaveEncashments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveEncashments_LeaveTypeId",
                schema: "tla",
                table: "LeaveEncashments",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePolicies_CustomerId_OrgId",
                schema: "tla",
                table: "LeavePolicies",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeavePolicies_LeaveTypeId",
                schema: "tla",
                table: "LeavePolicies",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_CustomerId_OrgId",
                schema: "tla",
                table: "LeaveTypes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_CustomerId_OrgId_Code",
                schema: "tla",
                table: "LeaveTypes",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_CustomerId_OrgId",
                schema: "tla",
                table: "OvertimeRequests",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Punches_CustomerId_OrgId",
                schema: "tla",
                table: "Punches",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Punches_CustomerId_OrgId_EmployeeId_PunchedAt",
                schema: "tla",
                table: "Punches",
                columns: new[] { "CustomerId", "OrgId", "EmployeeId", "PunchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RegularisationRequests_CustomerId_OrgId",
                schema: "tla",
                table: "RegularisationRequests",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosters_CustomerId_OrgId",
                schema: "tla",
                table: "ShiftRosters",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosters_CustomerId_OrgId_EmployeeId_FromDate_ToDate",
                schema: "tla",
                table: "ShiftRosters",
                columns: new[] { "CustomerId", "OrgId", "EmployeeId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosters_ShiftId",
                schema: "tla",
                table: "ShiftRosters",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRosters_WeeklyOffPolicyId",
                schema: "tla",
                table: "ShiftRosters",
                column: "WeeklyOffPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_CustomerId_OrgId",
                schema: "tla",
                table: "Shifts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_CustomerId_OrgId_Code",
                schema: "tla",
                table: "Shifts",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyOffPolicies_CustomerId_OrgId",
                schema: "tla",
                table: "WeeklyOffPolicies",
                columns: new[] { "CustomerId", "OrgId" });

            string[] tables =
            {
                "ApprovalSteps",
                "BiometricDeviceUsers",
                "CompOffCredits",
                "DailyAttendances",
                "ErrorLogs",
                "Holidays",
                "HolidayLists",
                "LeaveApplications",
                "LeaveBalances",
                "LeaveEncashments",
                "LeavePolicies",
                "LeaveTypes",
                "OvertimeRequests",
                "Punches",
                "RegularisationRequests",
                "Shifts",
                "ShiftRosters",
                "WeeklyOffPolicies"
            };

            foreach (string table in tables)
            {
                migrationBuilder.Sql($"ALTER TABLE tla.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE tla.\"{table}\" FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON tla."{table}"
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
                "BiometricDeviceUsers",
                "CompOffCredits",
                "DailyAttendances",
                "ErrorLogs",
                "Holidays",
                "HolidayLists",
                "LeaveApplications",
                "LeaveBalances",
                "LeaveEncashments",
                "LeavePolicies",
                "LeaveTypes",
                "OvertimeRequests",
                "Punches",
                "RegularisationRequests",
                "Shifts",
                "ShiftRosters",
                "WeeklyOffPolicies"
            };

            foreach (string table in tables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON tla.\"{table}\";");
            }
            migrationBuilder.DropTable(
                name: "ApprovalSteps",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "BiometricDeviceUsers",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "CompOffCredits",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "DailyAttendances",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "LeaveApplications",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "LeaveBalances",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "LeaveEncashments",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "LeavePolicies",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "OvertimeRequests",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "Punches",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "RegularisationRequests",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "ShiftRosters",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "HolidayLists",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "LeaveTypes",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "Shifts",
                schema: "tla");

            migrationBuilder.DropTable(
                name: "WeeklyOffPolicies",
                schema: "tla");
        }
    }
}
