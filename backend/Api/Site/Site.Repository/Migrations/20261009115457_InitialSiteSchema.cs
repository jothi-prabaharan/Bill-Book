using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Site.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialSiteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sit");

            migrationBuilder.CreateTable(
                name: "EditLog",
                schema: "sit",
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
                name: "ErrorLogs",
                schema: "sit",
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
                name: "LaborerClockEntries",
                schema: "sit",
                columns: table => new
                {
                    LaborerClockEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LaborerId = table.Column<long>(type: "bigint", nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    ClockInUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClockOutUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClockInLatitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    ClockInLongitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    ClockOutLatitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    ClockOutLongitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    SelfiePhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SitePhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegularHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    IsOtClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    OtReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LaborerClockEntries", x => x.LaborerClockEntryId);
                });

            migrationBuilder.CreateTable(
                name: "Laborers",
                schema: "sit",
                columns: table => new
                {
                    LaborerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    LaborerType = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TradeId = table.Column<long>(type: "bigint", nullable: false),
                    CostHourlyRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BillingHourlyRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Laborers", x => x.LaborerId);
                });

            migrationBuilder.CreateTable(
                name: "MarginRules",
                schema: "sit",
                columns: table => new
                {
                    MarginRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradeId = table.Column<long>(type: "bigint", nullable: true),
                    SiteId = table.Column<long>(type: "bigint", nullable: true),
                    MinLaborMarginPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MinMaterialMarginPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    SubcontractorMarkupPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
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
                    table.PrimaryKey("PK_MarginRules", x => x.MarginRuleId);
                });

            migrationBuilder.CreateTable(
                name: "SiteAssignments",
                schema: "sit",
                columns: table => new
                {
                    SiteAssignmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    LaborerId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_SiteAssignments", x => x.SiteAssignmentId);
                });

            migrationBuilder.CreateTable(
                name: "SiteBillingInvoiceLines",
                schema: "sit",
                columns: table => new
                {
                    SiteBillingInvoiceLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteBillingInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    LineType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_SiteBillingInvoiceLines", x => x.SiteBillingInvoiceLineId);
                });

            migrationBuilder.CreateTable(
                name: "SiteBillingInvoices",
                schema: "sit",
                columns: table => new
                {
                    SiteBillingInvoiceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BillingNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    BillingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SalesInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    TotalLaborAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalMaterialAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalBillableAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsFinalBilling = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_SiteBillingInvoices", x => x.SiteBillingInvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "SiteDispatchLines",
                schema: "sit",
                columns: table => new
                {
                    SiteDispatchLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteDispatchId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    ItemSerialId = table.Column<long>(type: "bigint", nullable: true),
                    DispatchedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_SiteDispatchLines", x => x.SiteDispatchLineId);
                });

            migrationBuilder.CreateTable(
                name: "SiteDispatches",
                schema: "sit",
                columns: table => new
                {
                    SiteDispatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DispatchNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    FromWarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ToWarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    DispatchDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_SiteDispatches", x => x.SiteDispatchId);
                });

            migrationBuilder.CreateTable(
                name: "SiteMaterialUsageLines",
                schema: "sit",
                columns: table => new
                {
                    SiteMaterialUsageLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteMaterialUsageId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    ItemSerialId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_SiteMaterialUsageLines", x => x.SiteMaterialUsageLineId);
                });

            migrationBuilder.CreateTable(
                name: "SiteMaterialUsages",
                schema: "sit",
                columns: table => new
                {
                    SiteMaterialUsageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsageNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    ReportedByLaborerId = table.Column<long>(type: "bigint", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_SiteMaterialUsages", x => x.SiteMaterialUsageId);
                });

            migrationBuilder.CreateTable(
                name: "SiteReturnLines",
                schema: "sit",
                columns: table => new
                {
                    SiteReturnLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteReturnId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    ItemSerialId = table.Column<long>(type: "bigint", nullable: true),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RestockWarehouseId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_SiteReturnLines", x => x.SiteReturnLineId);
                });

            migrationBuilder.CreateTable(
                name: "SiteReturns",
                schema: "sit",
                columns: table => new
                {
                    SiteReturnId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReturnNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    FromWarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ToWarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_SiteReturns", x => x.SiteReturnId);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                schema: "sit",
                columns: table => new
                {
                    SiteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientContactId = table.Column<long>(type: "bigint", nullable: false),
                    SiteAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    GeoFenceRadiusMeters = table.Column<int>(type: "integer", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedCompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualCompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_Sites", x => x.SiteId);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                schema: "sit",
                columns: table => new
                {
                    TradeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradeCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TradeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StandardHourlyRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StandardOvertimeHourlyRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Trades", x => x.TradeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_CustomerId_OrgId",
                schema: "sit",
                table: "EditLog",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_ChangedAt",
                schema: "sit",
                table: "EditLog",
                columns: new[] { "OrgId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_EntityName_ChangedAt",
                schema: "sit",
                table: "EditLog",
                columns: new[] { "OrgId", "EntityName", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_RootEntityName_RootEntityKey_ChangedAt",
                schema: "sit",
                table: "EditLog",
                columns: new[] { "OrgId", "RootEntityName", "RootEntityKey", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_UserId_ChangedAt",
                schema: "sit",
                table: "EditLog",
                columns: new[] { "OrgId", "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "sit",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "sit",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "sit",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LaborerClockEntries_CustomerId_OrgId",
                schema: "sit",
                table: "LaborerClockEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LaborerClockEntries_CustomerId_OrgId_SiteId_LaborerId_Clock~",
                schema: "sit",
                table: "LaborerClockEntries",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "LaborerId", "ClockInUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Laborers_CustomerId_OrgId",
                schema: "sit",
                table: "Laborers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Laborers_CustomerId_OrgId_Phone",
                schema: "sit",
                table: "Laborers",
                columns: new[] { "CustomerId", "OrgId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_Laborers_CustomerId_OrgId_UserId",
                schema: "sit",
                table: "Laborers",
                columns: new[] { "CustomerId", "OrgId", "UserId" },
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarginRules_CustomerId_OrgId",
                schema: "sit",
                table: "MarginRules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssignments_CustomerId_OrgId",
                schema: "sit",
                table: "SiteAssignments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssignments_CustomerId_OrgId_SiteId_LaborerId",
                schema: "sit",
                table: "SiteAssignments",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "LaborerId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteBillingInvoiceLines_CustomerId_OrgId",
                schema: "sit",
                table: "SiteBillingInvoiceLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteBillingInvoiceLines_CustomerId_OrgId_SiteBillingInvoice~",
                schema: "sit",
                table: "SiteBillingInvoiceLines",
                columns: new[] { "CustomerId", "OrgId", "SiteBillingInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteBillingInvoices_CustomerId_OrgId",
                schema: "sit",
                table: "SiteBillingInvoices",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteBillingInvoices_CustomerId_OrgId_BillingNumber",
                schema: "sit",
                table: "SiteBillingInvoices",
                columns: new[] { "CustomerId", "OrgId", "BillingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteDispatchLines_CustomerId_OrgId",
                schema: "sit",
                table: "SiteDispatchLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteDispatchLines_CustomerId_OrgId_SiteDispatchId",
                schema: "sit",
                table: "SiteDispatchLines",
                columns: new[] { "CustomerId", "OrgId", "SiteDispatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteDispatches_CustomerId_OrgId",
                schema: "sit",
                table: "SiteDispatches",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteDispatches_CustomerId_OrgId_DispatchNumber",
                schema: "sit",
                table: "SiteDispatches",
                columns: new[] { "CustomerId", "OrgId", "DispatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteMaterialUsageLines_CustomerId_OrgId",
                schema: "sit",
                table: "SiteMaterialUsageLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteMaterialUsageLines_CustomerId_OrgId_SiteMaterialUsageId",
                schema: "sit",
                table: "SiteMaterialUsageLines",
                columns: new[] { "CustomerId", "OrgId", "SiteMaterialUsageId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteMaterialUsages_CustomerId_OrgId",
                schema: "sit",
                table: "SiteMaterialUsages",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteMaterialUsages_CustomerId_OrgId_UsageNumber",
                schema: "sit",
                table: "SiteMaterialUsages",
                columns: new[] { "CustomerId", "OrgId", "UsageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteReturnLines_CustomerId_OrgId",
                schema: "sit",
                table: "SiteReturnLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteReturnLines_CustomerId_OrgId_SiteReturnId",
                schema: "sit",
                table: "SiteReturnLines",
                columns: new[] { "CustomerId", "OrgId", "SiteReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteReturns_CustomerId_OrgId",
                schema: "sit",
                table: "SiteReturns",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteReturns_CustomerId_OrgId_ReturnNumber",
                schema: "sit",
                table: "SiteReturns",
                columns: new[] { "CustomerId", "OrgId", "ReturnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CustomerId_OrgId",
                schema: "sit",
                table: "Sites",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CustomerId_OrgId_SiteCode",
                schema: "sit",
                table: "Sites",
                columns: new[] { "CustomerId", "OrgId", "SiteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CustomerId_OrgId_Status",
                schema: "sit",
                table: "Sites",
                columns: new[] { "CustomerId", "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_CustomerId_OrgId",
                schema: "sit",
                table: "Trades",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_CustomerId_OrgId_TradeCode",
                schema: "sit",
                table: "Trades",
                columns: new[] { "CustomerId", "OrgId", "TradeCode" },
                unique: true);

            foreach (string table in RlsTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE sit."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE sit."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON sit."{table}";
                    CREATE POLICY {policy}
                        ON sit."{table}"
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
                name: "EditLog",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "LaborerClockEntries",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "Laborers",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "MarginRules",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteAssignments",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteBillingInvoiceLines",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteBillingInvoices",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteDispatchLines",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteDispatches",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteMaterialUsageLines",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteMaterialUsages",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteReturnLines",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteReturns",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "Sites",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "Trades",
                schema: "sit");
        }

        private static readonly string[] RlsTables =
        [
            "ErrorLogs",
            "EditLog",
            "Trades",
            "Laborers",
            "Sites",
            "SiteAssignments",
            "MarginRules",
            "LaborerClockEntries",
            "SiteDispatches",
            "SiteDispatchLines",
            "SiteReturns",
            "SiteReturnLines",
            "SiteMaterialUsages",
            "SiteMaterialUsageLines",
            "SiteBillingInvoices",
            "SiteBillingInvoiceLines"
        ];
    }
}
