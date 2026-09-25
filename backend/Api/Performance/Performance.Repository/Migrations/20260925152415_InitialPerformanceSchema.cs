using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Performance.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialPerformanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "prf");

            migrationBuilder.CreateTable(
                name: "CompetencyGroups",
                schema: "prf",
                columns: table => new
                {
                    CompetencyGroupId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_CompetencyGroups", x => x.CompetencyGroupId);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "prf",
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
                name: "Goals",
                schema: "prf",
                columns: table => new
                {
                    GoalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewCycleId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Weightage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Measure = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GoalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_Goals", x => x.GoalId);
                });

            migrationBuilder.CreateTable(
                name: "RatingScales",
                schema: "prf",
                columns: table => new
                {
                    RatingScaleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RatingScales", x => x.RatingScaleId);
                });

            migrationBuilder.CreateTable(
                name: "Competencies",
                schema: "prf",
                columns: table => new
                {
                    CompetencyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetencyGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Competencies", x => x.CompetencyId);
                    table.ForeignKey(
                        name: "FK_Competencies_CompetencyGroups_CompetencyGroupId",
                        column: x => x.CompetencyGroupId,
                        principalSchema: "prf",
                        principalTable: "CompetencyGroups",
                        principalColumn: "CompetencyGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingLevels",
                schema: "prf",
                columns: table => new
                {
                    RatingLevelId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RatingScaleId = table.Column<long>(type: "bigint", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_RatingLevels", x => x.RatingLevelId);
                    table.ForeignKey(
                        name: "FK_RatingLevels_RatingScales_RatingScaleId",
                        column: x => x.RatingScaleId,
                        principalSchema: "prf",
                        principalTable: "RatingScales",
                        principalColumn: "RatingScaleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewCycles",
                schema: "prf",
                columns: table => new
                {
                    ReviewCycleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    CycleKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RatingScaleId = table.Column<long>(type: "bigint", nullable: false),
                    GoalWeightPercent = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CompetencyWeightPercent = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GoalSettingDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SelfEvaluationDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsSelfEvaluationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsPeerFeedbackEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsCalibrationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CycleStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ReviewCycles", x => x.ReviewCycleId);
                    table.ForeignKey(
                        name: "FK_ReviewCycles_RatingScales_RatingScaleId",
                        column: x => x.RatingScaleId,
                        principalSchema: "prf",
                        principalTable: "RatingScales",
                        principalColumn: "RatingScaleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CycleCompetencies",
                schema: "prf",
                columns: table => new
                {
                    CycleCompetencyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewCycleId = table.Column<long>(type: "bigint", nullable: false),
                    CompetencyId = table.Column<long>(type: "bigint", nullable: false),
                    GradeId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_CycleCompetencies", x => x.CycleCompetencyId);
                    table.ForeignKey(
                        name: "FK_CycleCompetencies_Competencies_CompetencyId",
                        column: x => x.CompetencyId,
                        principalSchema: "prf",
                        principalTable: "Competencies",
                        principalColumn: "CompetencyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CycleCompetencies_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalSchema: "prf",
                        principalTable: "ReviewCycles",
                        principalColumn: "ReviewCycleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceReviews",
                schema: "prf",
                columns: table => new
                {
                    PerformanceReviewId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewCycleId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CurrentStepSequence = table.Column<int>(type: "integer", nullable: false),
                    CurrentAssigneeEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    SelfSubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalGoalScore = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    FinalCompetencyScore = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    FinalRatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    RecommendedIncreasePercent = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsPromotionRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendedDesignationId = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmployeeAcknowledgementComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovalWorkflowName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_PerformanceReviews", x => x.PerformanceReviewId);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalSchema: "prf",
                        principalTable: "ReviewCycles",
                        principalColumn: "ReviewCycleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewEligibilities",
                schema: "prf",
                columns: table => new
                {
                    ReviewEligibilityId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewCycleId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: true),
                    DesignationId = table.Column<long>(type: "bigint", nullable: true),
                    GradeId = table.Column<long>(type: "bigint", nullable: true),
                    JoinedBeforeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsIncluded = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ReviewEligibilities", x => x.ReviewEligibilityId);
                    table.ForeignKey(
                        name: "FK_ReviewEligibilities_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalSchema: "prf",
                        principalTable: "ReviewCycles",
                        principalColumn: "ReviewCycleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationAdjustments",
                schema: "prf",
                columns: table => new
                {
                    CalibrationAdjustmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<long>(type: "bigint", nullable: false),
                    FromRatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    ToRatingLevelId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AdjustedByEmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    AdjustedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_CalibrationAdjustments", x => x.CalibrationAdjustmentId);
                    table.ForeignKey(
                        name: "FK_CalibrationAdjustments_PerformanceReviews_PerformanceReview~",
                        column: x => x.PerformanceReviewId,
                        principalSchema: "prf",
                        principalTable: "PerformanceReviews",
                        principalColumn: "PerformanceReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LevelReviews",
                schema: "prf",
                columns: table => new
                {
                    LevelReviewId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalStepId = table.Column<long>(type: "bigint", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReviewerEmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    RatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    IncreasePercent = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsPromotionRecommended = table.Column<bool>(type: "boolean", nullable: true),
                    RecommendedDesignationId = table.Column<long>(type: "bigint", nullable: true),
                    Comments = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LevelReviews", x => x.LevelReviewId);
                    table.ForeignKey(
                        name: "FK_LevelReviews_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalSchema: "prf",
                        principalTable: "PerformanceReviews",
                        principalColumn: "PerformanceReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeerFeedbacks",
                schema: "prf",
                columns: table => new
                {
                    PeerFeedbackId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<long>(type: "bigint", nullable: false),
                    ReviewerEmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    IsSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_PeerFeedbacks", x => x.PeerFeedbackId);
                    table.ForeignKey(
                        name: "FK_PeerFeedbacks_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalSchema: "prf",
                        principalTable: "PerformanceReviews",
                        principalColumn: "PerformanceReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceApprovalSteps",
                schema: "prf",
                columns: table => new
                {
                    PerformanceApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    StepStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCommentRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceApprovalSteps", x => x.PerformanceApprovalStepId);
                    table.ForeignKey(
                        name: "FK_PerformanceApprovalSteps_PerformanceReviews_PerformanceRevi~",
                        column: x => x.PerformanceReviewId,
                        principalSchema: "prf",
                        principalTable: "PerformanceReviews",
                        principalColumn: "PerformanceReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SelfEvaluations",
                schema: "prf",
                columns: table => new
                {
                    SelfEvaluationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PerformanceReviewId = table.Column<long>(type: "bigint", nullable: false),
                    OverallSelfRatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    Achievements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Challenges = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AreasToImprove = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TrainingNeeds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CareerAspirations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_SelfEvaluations", x => x.SelfEvaluationId);
                    table.ForeignKey(
                        name: "FK_SelfEvaluations_PerformanceReviews_PerformanceReviewId",
                        column: x => x.PerformanceReviewId,
                        principalSchema: "prf",
                        principalTable: "PerformanceReviews",
                        principalColumn: "PerformanceReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LevelCompetencyRatings",
                schema: "prf",
                columns: table => new
                {
                    LevelCompetencyRatingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LevelReviewId = table.Column<long>(type: "bigint", nullable: false),
                    CompetencyId = table.Column<long>(type: "bigint", nullable: false),
                    RatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Comments = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_LevelCompetencyRatings", x => x.LevelCompetencyRatingId);
                    table.ForeignKey(
                        name: "FK_LevelCompetencyRatings_LevelReviews_LevelReviewId",
                        column: x => x.LevelReviewId,
                        principalSchema: "prf",
                        principalTable: "LevelReviews",
                        principalColumn: "LevelReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LevelGoalRatings",
                schema: "prf",
                columns: table => new
                {
                    LevelGoalRatingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LevelReviewId = table.Column<long>(type: "bigint", nullable: false),
                    GoalId = table.Column<long>(type: "bigint", nullable: false),
                    RatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Comments = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_LevelGoalRatings", x => x.LevelGoalRatingId);
                    table.ForeignKey(
                        name: "FK_LevelGoalRatings_LevelReviews_LevelReviewId",
                        column: x => x.LevelReviewId,
                        principalSchema: "prf",
                        principalTable: "LevelReviews",
                        principalColumn: "LevelReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetencySelfAssessments",
                schema: "prf",
                columns: table => new
                {
                    CompetencySelfAssessmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SelfEvaluationId = table.Column<long>(type: "bigint", nullable: false),
                    CompetencyId = table.Column<long>(type: "bigint", nullable: false),
                    SelfRatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    Comments = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CompetencySelfAssessments", x => x.CompetencySelfAssessmentId);
                    table.ForeignKey(
                        name: "FK_CompetencySelfAssessments_SelfEvaluations_SelfEvaluationId",
                        column: x => x.SelfEvaluationId,
                        principalSchema: "prf",
                        principalTable: "SelfEvaluations",
                        principalColumn: "SelfEvaluationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalSelfAssessments",
                schema: "prf",
                columns: table => new
                {
                    GoalSelfAssessmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SelfEvaluationId = table.Column<long>(type: "bigint", nullable: false),
                    GoalId = table.Column<long>(type: "bigint", nullable: false),
                    SelfRatingLevelId = table.Column<long>(type: "bigint", nullable: true),
                    AchievementPercent = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_GoalSelfAssessments", x => x.GoalSelfAssessmentId);
                    table.ForeignKey(
                        name: "FK_GoalSelfAssessments_SelfEvaluations_SelfEvaluationId",
                        column: x => x.SelfEvaluationId,
                        principalSchema: "prf",
                        principalTable: "SelfEvaluations",
                        principalColumn: "SelfEvaluationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SelfEvidences",
                schema: "prf",
                columns: table => new
                {
                    SelfEvidenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoalSelfAssessmentId = table.Column<long>(type: "bigint", nullable: false),
                    AttachmentKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_SelfEvidences", x => x.SelfEvidenceId);
                    table.ForeignKey(
                        name: "FK_SelfEvidences_GoalSelfAssessments_GoalSelfAssessmentId",
                        column: x => x.GoalSelfAssessmentId,
                        principalSchema: "prf",
                        principalTable: "GoalSelfAssessments",
                        principalColumn: "GoalSelfAssessmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationAdjustments_CustomerId_OrgId",
                schema: "prf",
                table: "CalibrationAdjustments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationAdjustments_PerformanceReviewId",
                schema: "prf",
                table: "CalibrationAdjustments",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Competencies_CompetencyGroupId",
                schema: "prf",
                table: "Competencies",
                column: "CompetencyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Competencies_CustomerId_OrgId",
                schema: "prf",
                table: "Competencies",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyGroups_CustomerId_OrgId",
                schema: "prf",
                table: "CompetencyGroups",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetencySelfAssessments_CustomerId_OrgId",
                schema: "prf",
                table: "CompetencySelfAssessments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetencySelfAssessments_SelfEvaluationId",
                schema: "prf",
                table: "CompetencySelfAssessments",
                column: "SelfEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleCompetencies_CompetencyId",
                schema: "prf",
                table: "CycleCompetencies",
                column: "CompetencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleCompetencies_CustomerId_OrgId",
                schema: "prf",
                table: "CycleCompetencies",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CycleCompetencies_ReviewCycleId",
                schema: "prf",
                table: "CycleCompetencies",
                column: "ReviewCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "prf",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "prf",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "prf",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalSelfAssessments_CustomerId_OrgId",
                schema: "prf",
                table: "GoalSelfAssessments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalSelfAssessments_SelfEvaluationId",
                schema: "prf",
                table: "GoalSelfAssessments",
                column: "SelfEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_CustomerId_OrgId",
                schema: "prf",
                table: "Goals",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_CustomerId_OrgId_ReviewCycleId_EmployeeId",
                schema: "prf",
                table: "Goals",
                columns: new[] { "CustomerId", "OrgId", "ReviewCycleId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelCompetencyRatings_CustomerId_OrgId",
                schema: "prf",
                table: "LevelCompetencyRatings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelCompetencyRatings_LevelReviewId",
                schema: "prf",
                table: "LevelCompetencyRatings",
                column: "LevelReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_LevelGoalRatings_CustomerId_OrgId",
                schema: "prf",
                table: "LevelGoalRatings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelGoalRatings_LevelReviewId",
                schema: "prf",
                table: "LevelGoalRatings",
                column: "LevelReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_LevelReviews_CustomerId_OrgId",
                schema: "prf",
                table: "LevelReviews",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelReviews_PerformanceReviewId",
                schema: "prf",
                table: "LevelReviews",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerFeedbacks_CustomerId_OrgId",
                schema: "prf",
                table: "PeerFeedbacks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PeerFeedbacks_PerformanceReviewId",
                schema: "prf",
                table: "PeerFeedbacks",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceApprovalSteps_CustomerId_OrgId",
                schema: "prf",
                table: "PerformanceApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceApprovalSteps_PerformanceReviewId",
                schema: "prf",
                table: "PerformanceApprovalSteps",
                column: "PerformanceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_CustomerId_OrgId",
                schema: "prf",
                table: "PerformanceReviews",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_CustomerId_OrgId_ReviewCycleId_EmployeeId",
                schema: "prf",
                table: "PerformanceReviews",
                columns: new[] { "CustomerId", "OrgId", "ReviewCycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewCycleId",
                schema: "prf",
                table: "PerformanceReviews",
                column: "ReviewCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingLevels_CustomerId_OrgId",
                schema: "prf",
                table: "RatingLevels",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingLevels_RatingScaleId",
                schema: "prf",
                table: "RatingLevels",
                column: "RatingScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingScales_CustomerId_OrgId",
                schema: "prf",
                table: "RatingScales",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_CustomerId_OrgId",
                schema: "prf",
                table: "ReviewCycles",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_RatingScaleId",
                schema: "prf",
                table: "ReviewCycles",
                column: "RatingScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEligibilities_CustomerId_OrgId",
                schema: "prf",
                table: "ReviewEligibilities",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEligibilities_CustomerId_OrgId_ReviewCycleId_Employee~",
                schema: "prf",
                table: "ReviewEligibilities",
                columns: new[] { "CustomerId", "OrgId", "ReviewCycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEligibilities_ReviewCycleId",
                schema: "prf",
                table: "ReviewEligibilities",
                column: "ReviewCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfEvaluations_CustomerId_OrgId",
                schema: "prf",
                table: "SelfEvaluations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfEvaluations_CustomerId_OrgId_PerformanceReviewId",
                schema: "prf",
                table: "SelfEvaluations",
                columns: new[] { "CustomerId", "OrgId", "PerformanceReviewId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfEvaluations_PerformanceReviewId",
                schema: "prf",
                table: "SelfEvaluations",
                column: "PerformanceReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfEvidences_CustomerId_OrgId",
                schema: "prf",
                table: "SelfEvidences",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfEvidences_GoalSelfAssessmentId",
                schema: "prf",
                table: "SelfEvidences",
                column: "GoalSelfAssessmentId");

            string[] tables =
            {
                "CalibrationAdjustments",
                "Competencies",
                "CompetencyGroups",
                "CompetencySelfAssessments",
                "CycleCompetencies",
                "ErrorLogs",
                "Goals",
                "GoalSelfAssessments",
                "LevelCompetencyRatings",
                "LevelGoalRatings",
                "LevelReviews",
                "PeerFeedbacks",
                "PerformanceApprovalSteps",
                "PerformanceReviews",
                "RatingLevels",
                "RatingScales",
                "ReviewCycles",
                "ReviewEligibilities",
                "SelfEvaluations",
                "SelfEvidences"
            };

            foreach (string table in tables)
            {
                migrationBuilder.Sql($"ALTER TABLE prf.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE prf.\"{table}\" FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON prf."{table}"
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
                "CalibrationAdjustments",
                "Competencies",
                "CompetencyGroups",
                "CompetencySelfAssessments",
                "CycleCompetencies",
                "ErrorLogs",
                "Goals",
                "GoalSelfAssessments",
                "LevelCompetencyRatings",
                "LevelGoalRatings",
                "LevelReviews",
                "PeerFeedbacks",
                "PerformanceApprovalSteps",
                "PerformanceReviews",
                "RatingLevels",
                "RatingScales",
                "ReviewCycles",
                "ReviewEligibilities",
                "SelfEvaluations",
                "SelfEvidences"
            };

            foreach (string table in tables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON prf.\"{table}\";");
            }
            migrationBuilder.DropTable(
                name: "CalibrationAdjustments",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "CompetencySelfAssessments",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "CycleCompetencies",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "Goals",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "LevelCompetencyRatings",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "LevelGoalRatings",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "PeerFeedbacks",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "PerformanceApprovalSteps",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "RatingLevels",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "ReviewEligibilities",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "SelfEvidences",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "Competencies",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "LevelReviews",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "GoalSelfAssessments",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "CompetencyGroups",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "SelfEvaluations",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "PerformanceReviews",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "ReviewCycles",
                schema: "prf");

            migrationBuilder.DropTable(
                name: "RatingScales",
                schema: "prf");
        }
    }
}
