using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fee.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialFeeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fee");

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "fee",
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
                name: "FeeHeads",
                schema: "fee",
                columns: table => new
                {
                    FeeHeadId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IncomeAccountId = table.Column<long>(type: "bigint", nullable: true),
                    IsRefundable = table.Column<bool>(type: "boolean", nullable: false),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
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
                    table.PrimaryKey("PK_FeeHeads", x => x.FeeHeadId);
                });

            migrationBuilder.CreateTable(
                name: "FeeReceipts",
                schema: "fee",
                columns: table => new
                {
                    FeeReceiptId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnallocatedAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_FeeReceipts", x => x.FeeReceiptId);
                    table.CheckConstraint("chk_fee_receipt_amounts", "\"Amount\" > 0 AND \"UnallocatedAmount\" >= 0 AND \"UnallocatedAmount\" <= \"Amount\"");
                });

            migrationBuilder.CreateTable(
                name: "FeeStructures",
                schema: "fee",
                columns: table => new
                {
                    FeeStructureId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYearId = table.Column<long>(type: "bigint", nullable: false),
                    SchoolClassId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstMonth = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_FeeStructures", x => x.FeeStructureId);
                });

            migrationBuilder.CreateTable(
                name: "FeeConcessions",
                schema: "fee",
                columns: table => new
                {
                    FeeConcessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    FeeHeadId = table.Column<long>(type: "bigint", nullable: false),
                    ConcessionKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_FeeConcessions", x => x.FeeConcessionId);
                    table.CheckConstraint("chk_fee_concession", "\"Value\" > 0 AND (\"ConcessionKind\" <> 'Percent' OR \"Value\" <= 100) AND \"ValidTo\" >= \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_FeeConcessions_FeeHeads_FeeHeadId",
                        column: x => x.FeeHeadId,
                        principalSchema: "fee",
                        principalTable: "FeeHeads",
                        principalColumn: "FeeHeadId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeDemands",
                schema: "fee",
                columns: table => new
                {
                    FeeDemandId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DemandNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    StudentId = table.Column<long>(type: "bigint", nullable: false),
                    EnrolmentId = table.Column<long>(type: "bigint", nullable: false),
                    FeeStructureId = table.Column<long>(type: "bigint", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    DemandDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConcessionAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_FeeDemands", x => x.FeeDemandId);
                    table.CheckConstraint("chk_fee_demand_amounts", "\"NetAmount\" = \"TotalAmount\" - \"ConcessionAmount\" AND \"NetAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"PaidAmount\" <= \"NetAmount\"");
                    table.CheckConstraint("chk_fee_demand_posted_numbered", "\"DocumentStatus\" = 'Draft' OR \"DemandNo\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_FeeDemands_FeeStructures_FeeStructureId",
                        column: x => x.FeeStructureId,
                        principalSchema: "fee",
                        principalTable: "FeeStructures",
                        principalColumn: "FeeStructureId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructureLines",
                schema: "fee",
                columns: table => new
                {
                    FeeStructureLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeeStructureId = table.Column<long>(type: "bigint", nullable: false),
                    FeeHeadId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDay = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_FeeStructureLines", x => x.FeeStructureLineId);
                    table.CheckConstraint("chk_fee_structure_line", "\"Amount\" > 0 AND \"DueDay\" BETWEEN 1 AND 28");
                    table.ForeignKey(
                        name: "FK_FeeStructureLines_FeeHeads_FeeHeadId",
                        column: x => x.FeeHeadId,
                        principalSchema: "fee",
                        principalTable: "FeeHeads",
                        principalColumn: "FeeHeadId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeStructureLines_FeeStructures_FeeStructureId",
                        column: x => x.FeeStructureId,
                        principalSchema: "fee",
                        principalTable: "FeeStructures",
                        principalColumn: "FeeStructureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeeDemandLines",
                schema: "fee",
                columns: table => new
                {
                    FeeDemandLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeeDemandId = table.Column<long>(type: "bigint", nullable: false),
                    FeeHeadId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConcessionAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_FeeDemandLines", x => x.FeeDemandLineId);
                    table.CheckConstraint("chk_fee_demand_line", "\"Amount\" > 0 AND \"ConcessionAmount\" >= 0 AND \"ConcessionAmount\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_FeeDemandLines_FeeDemands_FeeDemandId",
                        column: x => x.FeeDemandId,
                        principalSchema: "fee",
                        principalTable: "FeeDemands",
                        principalColumn: "FeeDemandId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeeDemandLines_FeeHeads_FeeHeadId",
                        column: x => x.FeeHeadId,
                        principalSchema: "fee",
                        principalTable: "FeeHeads",
                        principalColumn: "FeeHeadId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeReceiptAllocations",
                schema: "fee",
                columns: table => new
                {
                    FeeReceiptAllocationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeeReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    FeeDemandId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_FeeReceiptAllocations", x => x.FeeReceiptAllocationId);
                    table.CheckConstraint("chk_fee_allocation", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_FeeReceiptAllocations_FeeDemands_FeeDemandId",
                        column: x => x.FeeDemandId,
                        principalSchema: "fee",
                        principalTable: "FeeDemands",
                        principalColumn: "FeeDemandId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeReceiptAllocations_FeeReceipts_FeeReceiptId",
                        column: x => x.FeeReceiptId,
                        principalSchema: "fee",
                        principalTable: "FeeReceipts",
                        principalColumn: "FeeReceiptId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "fee",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "fee",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "fee",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeConcessions_CustomerId_OrgId",
                schema: "fee",
                table: "FeeConcessions",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeConcessions_FeeHeadId",
                schema: "fee",
                table: "FeeConcessions",
                column: "FeeHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeConcessions_OrgId_StudentId",
                schema: "fee",
                table: "FeeConcessions",
                columns: new[] { "OrgId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemandLines_CustomerId_OrgId",
                schema: "fee",
                table: "FeeDemandLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemandLines_FeeDemandId",
                schema: "fee",
                table: "FeeDemandLines",
                column: "FeeDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemandLines_FeeHeadId",
                schema: "fee",
                table: "FeeDemandLines",
                column: "FeeHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemands_CustomerId_OrgId",
                schema: "fee",
                table: "FeeDemands",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemands_CustomerId_OrgId_DemandNo",
                schema: "fee",
                table: "FeeDemands",
                columns: new[] { "CustomerId", "OrgId", "DemandNo" },
                unique: true,
                filter: "\"DemandNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemands_EnrolmentId_FeeStructureId_PeriodKey",
                schema: "fee",
                table: "FeeDemands",
                columns: new[] { "EnrolmentId", "FeeStructureId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemands_FeeStructureId",
                schema: "fee",
                table: "FeeDemands",
                column: "FeeStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeDemands_OrgId_ContactId_DocumentStatus",
                schema: "fee",
                table: "FeeDemands",
                columns: new[] { "OrgId", "ContactId", "DocumentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeHeads_CustomerId_OrgId",
                schema: "fee",
                table: "FeeHeads",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeHeads_CustomerId_OrgId_Code",
                schema: "fee",
                table: "FeeHeads",
                columns: new[] { "CustomerId", "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeReceiptAllocations_CustomerId_OrgId",
                schema: "fee",
                table: "FeeReceiptAllocations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeReceiptAllocations_FeeDemandId",
                schema: "fee",
                table: "FeeReceiptAllocations",
                column: "FeeDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeReceiptAllocations_FeeReceiptId_FeeDemandId",
                schema: "fee",
                table: "FeeReceiptAllocations",
                columns: new[] { "FeeReceiptId", "FeeDemandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeReceipts_CustomerId_OrgId",
                schema: "fee",
                table: "FeeReceipts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeReceipts_CustomerId_OrgId_ReceiptNo",
                schema: "fee",
                table: "FeeReceipts",
                columns: new[] { "CustomerId", "OrgId", "ReceiptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeReceipts_OrgId_ContactId",
                schema: "fee",
                table: "FeeReceipts",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureLines_CustomerId_OrgId",
                schema: "fee",
                table: "FeeStructureLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureLines_FeeHeadId",
                schema: "fee",
                table: "FeeStructureLines",
                column: "FeeHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureLines_FeeStructureId",
                schema: "fee",
                table: "FeeStructureLines",
                column: "FeeStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_CustomerId_OrgId",
                schema: "fee",
                table: "FeeStructures",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_CustomerId_OrgId_AcademicYearId_SchoolClassId~",
                schema: "fee",
                table: "FeeStructures",
                columns: new[] { "CustomerId", "OrgId", "AcademicYearId", "SchoolClassId", "Name" },
                unique: true);

            // Row-level security on every fee table (S4, TK-64), in the same
            // migration that creates them, in TK-71's form: ENABLE, FORCE, one
            // policy on CustomerId and OrgId, and NULLIF so a request with no
            // tenant sees nothing rather than failing on ''::uuid. No WITH
            // CHECK, so USING is also the write check.
            foreach (string table in RlsTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE fee."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE fee."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON fee."{table}";
                    CREATE POLICY {policy}
                        ON fee."{table}"
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
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeConcessions",
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeDemandLines",
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeReceiptAllocations",
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeStructureLines",
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeDemands",
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeReceipts",
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeHeads",
                schema: "fee");

            migrationBuilder.DropTable(
                name: "FeeStructures",
                schema: "fee");
        }

        private static readonly string[] RlsTables =
        [
            "ErrorLogs",
            "FeeHeads",
            "FeeReceipts",
            "FeeStructures",
            "FeeConcessions",
            "FeeDemands",
            "FeeStructureLines",
            "FeeDemandLines",
            "FeeReceiptAllocations",
        ];
    }
}
