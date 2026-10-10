using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_01_InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sal");

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "sal",
                columns: table => new
                {
                    SalesApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    StepStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ActedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCommentRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.SalesApprovalStepId);
                });

            migrationBuilder.CreateTable(
                name: "EInvoices",
                schema: "sal",
                columns: table => new
                {
                    EInvoiceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Irn = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AckNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AckDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SignedQrCode = table.Column<string>(type: "text", nullable: true),
                    SignedInvoice = table.Column<string>(type: "text", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastErrorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CancelRemark = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_EInvoices", x => x.EInvoiceId);
                    table.CheckConstraint("chk_einvoices_attempts", "\"Attempts\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "EditLog",
                schema: "sal",
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
                schema: "sal",
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
                name: "EwayBills",
                schema: "sal",
                columns: table => new
                {
                    EwayBillId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    Origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EwbNo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    EwbDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TransportMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    VehicleNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TransporterId = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    TransporterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DistanceKm = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastErrorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_EwayBills", x => x.EwayBillId);
                    table.CheckConstraint("chk_ewaybills_attempts", "\"Attempts\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Floors",
                schema: "sal",
                columns: table => new
                {
                    FloorId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Floors", x => x.FloorId);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "sal",
                columns: table => new
                {
                    QuoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    CustomerResponse = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponseNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CurrentStepLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentApproverRoleId = table.Column<int>(type: "integer", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true),
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    CustomFields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.QuoteId);
                    table.CheckConstraint("chk_quotes_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_quotes_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_quotes_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_quotes_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_quotes_response_stamp", "(\"CustomerResponse\" = 'None') = (\"RespondedAt\" IS NULL)");
                    table.CheckConstraint("chk_quotes_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_quotes_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_quotes_type", "\"TransactionTypeCode\" IN ('QTE')");
                    table.CheckConstraint("chk_quotes_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "ReminderLogs",
                schema: "sal",
                columns: table => new
                {
                    ReminderLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReminderProfileId = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_ReminderLogs", x => x.ReminderLogId);
                });

            migrationBuilder.CreateTable(
                name: "ReminderProfiles",
                schema: "sal",
                columns: table => new
                {
                    ReminderProfileId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DaysOverdueTrigger = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ReminderProfiles", x => x.ReminderProfileId);
                });

            migrationBuilder.CreateTable(
                name: "SalesRegister",
                schema: "sal",
                columns: table => new
                {
                    SalesRegisterId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    SupplyType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    ReverseCharge = table.Column<bool>(type: "boolean", nullable: false),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    GstRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UqcCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TaxableAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxableAmountBase = table.Column<decimal>(type: "numeric", nullable: false),
                    OriginalInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalInvoiceNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OriginalInvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_SalesRegister", x => x.SalesRegisterId);
                    table.CheckConstraint("chk_salesregister_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                });

            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                schema: "sal",
                columns: table => new
                {
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FloorId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MergedIntoTableId = table.Column<long>(type: "bigint", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RestaurantTables", x => x.RestaurantTableId);
                    table.CheckConstraint("chk_restaurant_table_not_self", "\"MergedIntoTableId\" IS NULL OR \"MergedIntoTableId\" <> \"RestaurantTableId\"");
                    table.CheckConstraint("chk_restaurant_table_seats", "\"Seats\" BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_RestaurantTables_Floors_FloorId",
                        column: x => x.FloorId,
                        principalSchema: "sal",
                        principalTable: "Floors",
                        principalColumn: "FloorId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantTables_RestaurantTables_MergedIntoTableId",
                        column: x => x.MergedIntoTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteDetails",
                schema: "sal",
                columns: table => new
                {
                    QuoteDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteDetails", x => x.QuoteDetailId);
                    table.CheckConstraint("chk_quotedetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_quotedetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_quotedetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_quotedetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_quotedetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_quotedetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_quotedetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_quotedetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_quotedetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_quotedetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_QuoteDetails_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "sal",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                schema: "sal",
                columns: table => new
                {
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FulfilmentStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ShortCloseReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreditOverrideStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    DiscountOverrideStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CurrentStepLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentApproverRoleId = table.Column<int>(type: "integer", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true),
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    CustomFields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.SalesOrderId);
                    table.CheckConstraint("chk_salesorders_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_salesorders_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_salesorders_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_salesorders_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_salesorders_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_salesorders_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_salesorders_type", "\"TransactionTypeCode\" IN ('SOR')");
                    table.CheckConstraint("chk_salesorders_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SalesOrders_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "sal",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrders",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NumberingSeriesId = table.Column<long>(type: "bigint", nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OrderType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: true),
                    WaiterUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    Guests = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    MergedIntoOrderId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_RestaurantOrders", x => x.RestaurantOrderId);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_RestaurantOrders_MergedIntoOrderId",
                        column: x => x.MergedIntoOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_RestaurantTables_RestaurantTableId",
                        column: x => x.RestaurantTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuoteDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    QuoteDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteDetailTaxes", x => x.QuoteDetailTaxId);
                    table.CheckConstraint("chk_quotedetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_QuoteDetailTaxes_QuoteDetails_QuoteDetailId",
                        column: x => x.QuoteDetailId,
                        principalSchema: "sal",
                        principalTable: "QuoteDetails",
                        principalColumn: "QuoteDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallans",
                schema: "sal",
                columns: table => new
                {
                    DeliveryChallanId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: true),
                    ChallanType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DispatchDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VehicleNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TransporterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EwayBillNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EwayBillDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CurrentStepLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentApproverRoleId = table.Column<int>(type: "integer", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true),
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    CustomFields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChallans", x => x.DeliveryChallanId);
                    table.CheckConstraint("chk_deliverychallans_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_deliverychallans_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_deliverychallans_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_deliverychallans_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_deliverychallans_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_deliverychallans_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_deliverychallans_type", "\"TransactionTypeCode\" IN ('DLC')");
                    table.CheckConstraint("chk_deliverychallans_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DeliveryChallans_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "sal",
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderDetails",
                schema: "sal",
                columns: table => new
                {
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    DeliveredQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    InvoicedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderDetails", x => x.SalesOrderDetailId);
                    table.CheckConstraint("chk_salesorderdetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_salesorderdetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_salesorderdetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_salesorderdetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_salesorderdetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_salesorderdetails_invoiced", "\"InvoicedQuantity\" >= 0 AND \"InvoicedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_salesorderdetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_salesorderdetails_quantities", "\"ReservedQuantity\" >= 0 AND \"DeliveredQuantity\" >= 0 AND \"DeliveredQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_salesorderdetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_salesorderdetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_salesorderdetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_salesorderdetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_SalesOrderDetails_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "sal",
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AggregatorOrders",
                schema: "sal",
                columns: table => new
                {
                    AggregatorOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Aggregator = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExternalOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AggregatorTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_AggregatorOrders", x => x.AggregatorOrderId);
                    table.ForeignKey(
                        name: "FK_AggregatorOrders_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kots",
                schema: "sal",
                columns: table => new
                {
                    KotId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    KotNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NumberingSeriesId = table.Column<long>(type: "bigint", nullable: false),
                    Station = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PrintedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Kots", x => x.KotId);
                    table.ForeignKey(
                        name: "FK_Kots_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TableReservations",
                schema: "sal",
                columns: table => new
                {
                    TableReservationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuestName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    ReservedFor = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: true),
                    SeatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("PK_TableReservations", x => x.TableReservationId);
                    table.CheckConstraint("chk_table_reservation_duration", "\"DurationMinutes\" BETWEEN 15 AND 720");
                    table.CheckConstraint("chk_table_reservation_party", "\"PartySize\" BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_TableReservations_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableReservations_RestaurantTables_RestaurantTableId",
                        column: x => x.RestaurantTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaitingListEntries",
                schema: "sal",
                columns: table => new
                {
                    WaitingListEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuestName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RestaurantTableId = table.Column<long>(type: "bigint", nullable: true),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("PK_WaitingListEntries", x => x.WaitingListEntryId);
                    table.CheckConstraint("chk_waiting_list_party", "\"PartySize\" BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_WaitingListEntries_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaitingListEntries_RestaurantTables_RestaurantTableId",
                        column: x => x.RestaurantTableId,
                        principalSchema: "sal",
                        principalTable: "RestaurantTables",
                        principalColumn: "RestaurantTableId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "sal",
                columns: table => new
                {
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<long>(type: "bigint", nullable: true),
                    SalesOrderId = table.Column<long>(type: "bigint", nullable: true),
                    DeliveryChallanId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentTermId = table.Column<long>(type: "bigint", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TillId = table.Column<long>(type: "bigint", nullable: true),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TenderedAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    ChangeAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: true),
                    TransportMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    VehicleNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TransporterId = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    TransporterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TransportDistanceKm = table.Column<int>(type: "integer", nullable: true),
                    CreditOverrideStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    DiscountOverrideStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CurrentStepLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentApproverRoleId = table.Column<int>(type: "integer", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true),
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    CustomFields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                    table.CheckConstraint("chk_invoices_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_invoices_due_date", "(\"TransactionTypeCode\" <> 'INV') OR (\"DueDate\" IS NOT NULL)");
                    table.CheckConstraint("chk_invoices_pos_fields", "(\"TransactionTypeCode\" <> 'POS') OR (\"TillId\" IS NOT NULL AND \"PaymentMode\" IS NOT NULL)");
                    table.CheckConstraint("chk_invoices_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_invoices_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_invoices_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_invoices_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_invoices_tender_non_negative", "(\"TenderedAmount\" IS NULL OR \"TenderedAmount\" >= 0) AND (\"ChangeAmount\" IS NULL OR \"ChangeAmount\" >= 0)");
                    table.CheckConstraint("chk_invoices_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_invoices_type", "\"TransactionTypeCode\" IN ('INV', 'POS')");
                    table.CheckConstraint("chk_invoices_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Invoices_DeliveryChallans_DeliveryChallanId",
                        column: x => x.DeliveryChallanId,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallans",
                        principalColumn: "DeliveryChallanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "sal",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "sal",
                        principalTable: "SalesOrders",
                        principalColumn: "SalesOrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallanDetails",
                schema: "sal",
                columns: table => new
                {
                    DeliveryChallanDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryChallanId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    InvoicedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: false),
                    UqcCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChallanDetails", x => x.DeliveryChallanDetailId);
                    table.CheckConstraint("chk_deliverychallandetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_deliverychallandetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_deliverychallandetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_deliverychallandetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_deliverychallandetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_deliverychallandetails_invoiced", "\"InvoicedQuantity\" >= 0 AND \"InvoicedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_deliverychallandetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_deliverychallandetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_deliverychallandetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_deliverychallandetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_deliverychallandetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetails_DeliveryChallans_DeliveryChallanId",
                        column: x => x.DeliveryChallanId,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallans",
                        principalColumn: "DeliveryChallanId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetails_SalesOrderDetails_SalesOrderDetailId",
                        column: x => x.SalesOrderDetailId,
                        principalSchema: "sal",
                        principalTable: "SalesOrderDetails",
                        principalColumn: "SalesOrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    SalesOrderDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderDetailTaxes", x => x.SalesOrderDetailTaxId);
                    table.CheckConstraint("chk_salesorderdetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_SalesOrderDetailTaxes_SalesOrderDetails_SalesOrderDetailId",
                        column: x => x.SalesOrderDetailId,
                        principalSchema: "sal",
                        principalTable: "SalesOrderDetails",
                        principalColumn: "SalesOrderDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                schema: "sal",
                columns: table => new
                {
                    CreditNoteId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTypeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactGstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    PlaceOfSupplyStateId = table.Column<int>(type: "integer", nullable: false),
                    IsInterState = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    CessAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    RoundOffAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TotalAmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CurrentStepLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentApproverRoleId = table.Column<int>(type: "integer", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true),
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    CustomFields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNotes", x => x.CreditNoteId);
                    table.CheckConstraint("chk_creditnotes_amounts_non_negative", "\"SubTotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"CgstAmount\" >= 0 AND \"SgstAmount\" >= 0 AND \"IgstAmount\" >= 0 AND \"CessAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"TotalAmountBase\" >= 0");
                    table.CheckConstraint("chk_creditnotes_posted_requires_stamp", "\"Status\" <> 'Posted' OR \"PostedAt\" IS NOT NULL");
                    table.CheckConstraint("chk_creditnotes_posted_stamp", "(\"Status\" IN ('Posted', 'Void')) OR \"PostedAt\" IS NULL");
                    table.CheckConstraint("chk_creditnotes_rate_positive", "\"ExchangeRate\" > 0");
                    table.CheckConstraint("chk_creditnotes_tax_split", "(\"IsInterState\" AND \"CgstAmount\" = 0 AND \"SgstAmount\" = 0) OR (NOT \"IsInterState\" AND \"IgstAmount\" = 0)");
                    table.CheckConstraint("chk_creditnotes_total", "\"TotalAmount\" = \"TaxableAmount\" + \"CgstAmount\" + \"SgstAmount\" + \"IgstAmount\" + \"CessAmount\" + \"RoundOffAmount\"");
                    table.CheckConstraint("chk_creditnotes_type", "\"TransactionTypeCode\" IN ('CRN')");
                    table.CheckConstraint("chk_creditnotes_void_stamp", "(\"Status\" = 'Void') = (\"VoidedAt\" IS NOT NULL) AND (\"VoidedAt\" IS NOT NULL) = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CreditNotes_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetails",
                schema: "sal",
                columns: table => new
                {
                    InvoiceDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    SalesOrderDetailId = table.Column<long>(type: "bigint", nullable: true),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: false),
                    UqcCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDetails", x => x.InvoiceDetailId);
                    table.CheckConstraint("chk_invoicedetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_invoicedetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_invoicedetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_invoicedetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_invoicedetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_invoicedetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_invoicedetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_invoicedetails_returned", "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("chk_invoicedetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_invoicedetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_invoicedetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_SalesOrderDetails_SalesOrderDetailId",
                        column: x => x.SalesOrderDetailId,
                        principalSchema: "sal",
                        principalTable: "SalesOrderDetails",
                        principalColumn: "SalesOrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceProjectItems",
                schema: "sal",
                columns: table => new
                {
                    InvoiceProjectItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_InvoiceProjectItems", x => x.InvoiceProjectItemId);
                    table.ForeignKey(
                        name: "FK_InvoiceProjectItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceTenders",
                schema: "sal",
                columns: table => new
                {
                    InvoiceTenderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_InvoiceTenders", x => x.InvoiceTenderId);
                    table.CheckConstraint("chk_invoicetender_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceTenders_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringInvoiceSchedules",
                schema: "sal",
                columns: table => new
                {
                    RecurringInvoiceScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Every = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxOccurrences = table.Column<int>(type: "integer", nullable: true),
                    NextOccurrenceIndex = table.Column<int>(type: "integer", nullable: false),
                    NextRunDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OccurrencesCreated = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AutoPost = table.Column<bool>(type: "boolean", nullable: false),
                    PlaceOfSupplyStateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RecurringInvoiceSchedules", x => x.RecurringInvoiceScheduleId);
                    table.ForeignKey(
                        name: "FK_RecurringInvoiceSchedules_Invoices_TemplateInvoiceId",
                        column: x => x.TemplateInvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantBills",
                schema: "sal",
                columns: table => new
                {
                    RestaurantBillId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceChargePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ServiceChargeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TipAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TipBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    BilledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LoyaltyPoints = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RestaurantBills", x => x.RestaurantBillId);
                    table.CheckConstraint("chk_restaurant_bill_tip", "\"TipAmount\" >= 0 AND (\"TipAmount\" = 0 OR \"TipBankAccountId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RestaurantBills_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryChallanDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    DeliveryChallanDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryChallanDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryChallanDetailTaxes", x => x.DeliveryChallanDetailTaxId);
                    table.CheckConstraint("chk_deliverychallandetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_DeliveryChallanDetailTaxes_DeliveryChallanDetails_DeliveryC~",
                        column: x => x.DeliveryChallanDetailId,
                        principalSchema: "sal",
                        principalTable: "DeliveryChallanDetails",
                        principalColumn: "DeliveryChallanDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditNoteDetails",
                schema: "sal",
                columns: table => new
                {
                    CreditNoteDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditNoteId = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceDetailId = table.Column<long>(type: "bigint", nullable: false),
                    UqcCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,6)", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    LineNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNoteDetails", x => x.CreditNoteDetailId);
                    table.CheckConstraint("chk_creditnotedetails_amounts_non_negative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"TaxableAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"LineTotal\" >= 0");
                    table.CheckConstraint("chk_creditnotedetails_base_quantity", "\"BaseQuantity\" = round(\"Quantity\" * \"ConversionFactor\", 6)");
                    table.CheckConstraint("chk_creditnotedetails_describes", "\"ItemId\" IS NOT NULL OR \"Description\" IS NOT NULL");
                    table.CheckConstraint("chk_creditnotedetails_discount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\"");
                    table.CheckConstraint("chk_creditnotedetails_free_text", "\"ItemId\" IS NOT NULL OR (\"AccountId\" IS NOT NULL AND \"LineType\" <> 'Stock')");
                    table.CheckConstraint("chk_creditnotedetails_line_type", "(\"LineType\" <> 'Expense' OR \"AccountId\" IS NOT NULL) AND (\"LineType\" <> 'Capital' OR \"FixedAssetCategoryId\" IS NOT NULL) AND (\"LineType\" <> 'Stock' OR \"ItemId\" IS NOT NULL)");
                    table.CheckConstraint("chk_creditnotedetails_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("chk_creditnotedetails_tax_master", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxMasterId\" IS NULL");
                    table.CheckConstraint("chk_creditnotedetails_total", "\"LineTotal\" = \"TaxableAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("chk_creditnotedetails_untaxed", "\"TaxTreatment\" IN ('Taxable', 'ZeroRated') OR \"TaxAmount\" = 0");
                    table.ForeignKey(
                        name: "FK_CreditNoteDetails_CreditNotes_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalSchema: "sal",
                        principalTable: "CreditNotes",
                        principalColumn: "CreditNoteId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditNoteDetails_InvoiceDetails_InvoiceDetailId",
                        column: x => x.InvoiceDetailId,
                        principalSchema: "sal",
                        principalTable: "InvoiceDetails",
                        principalColumn: "InvoiceDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    InvoiceDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDetailTaxes", x => x.InvoiceDetailTaxId);
                    table.CheckConstraint("chk_invoicedetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_InvoiceDetailTaxes_InvoiceDetails_InvoiceDetailId",
                        column: x => x.InvoiceDetailId,
                        principalSchema: "sal",
                        principalTable: "InvoiceDetails",
                        principalColumn: "InvoiceDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringInvoiceRuns",
                schema: "sal",
                columns: table => new
                {
                    RecurringInvoiceRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecurringInvoiceScheduleId = table.Column<long>(type: "bigint", nullable: false),
                    OccurrenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OccurrenceIndex = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Posted = table.Column<bool>(type: "boolean", nullable: false),
                    PostError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RecurringInvoiceRuns", x => x.RecurringInvoiceRunId);
                    table.ForeignKey(
                        name: "FK_RecurringInvoiceRuns_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sal",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringInvoiceRuns_RecurringInvoiceSchedules_RecurringInv~",
                        column: x => x.RecurringInvoiceScheduleId,
                        principalSchema: "sal",
                        principalTable: "RecurringInvoiceSchedules",
                        principalColumn: "RecurringInvoiceScheduleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrderLines",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemVariantId = table.Column<long>(type: "bigint", nullable: true),
                    VariantName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PortionFactor = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    Station = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    KitchenStatus = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    UomId = table.Column<long>(type: "bigint", nullable: false),
                    IsPriceInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    TaxTreatment = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IsStocked = table.Column<bool>(type: "boolean", nullable: false),
                    RestaurantBillId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_RestaurantOrderLines", x => x.RestaurantOrderLineId);
                    table.CheckConstraint("chk_restaurant_order_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_RestaurantBills_RestaurantBillId",
                        column: x => x.RestaurantBillId,
                        principalSchema: "sal",
                        principalTable: "RestaurantBills",
                        principalColumn: "RestaurantBillId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_RestaurantOrders_RestaurantOrderId",
                        column: x => x.RestaurantOrderId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrders",
                        principalColumn: "RestaurantOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditNoteDetailTaxes",
                schema: "sal",
                columns: table => new
                {
                    CreditNoteDetailTaxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditNoteDetailId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                    AmountBase = table.Column<decimal>(type: "numeric(28,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNoteDetailTaxes", x => x.CreditNoteDetailTaxId);
                    table.CheckConstraint("chk_creditnotedetailtaxes_non_negative", "\"Rate\" >= 0 AND \"TaxableAmount\" >= 0 AND \"Amount\" >= 0 AND \"AmountBase\" >= 0");
                    table.ForeignKey(
                        name: "FK_CreditNoteDetailTaxes_CreditNoteDetails_CreditNoteDetailId",
                        column: x => x.CreditNoteDetailId,
                        principalSchema: "sal",
                        principalTable: "CreditNoteDetails",
                        principalColumn: "CreditNoteDetailId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KotLines",
                schema: "sal",
                columns: table => new
                {
                    KotLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KotId = table.Column<long>(type: "bigint", nullable: false),
                    RestaurantOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_KotLines", x => x.KotLineId);
                    table.ForeignKey(
                        name: "FK_KotLines_Kots_KotId",
                        column: x => x.KotId,
                        principalSchema: "sal",
                        principalTable: "Kots",
                        principalColumn: "KotId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotLines_RestaurantOrderLines_RestaurantOrderLineId",
                        column: x => x.RestaurantOrderLineId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrderLines",
                        principalColumn: "RestaurantOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrderLineModifiers",
                schema: "sal",
                columns: table => new
                {
                    RestaurantOrderLineModifierId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    ModifierId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceChange = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LinkedItemId = table.Column<long>(type: "bigint", nullable: true),
                    LinkedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
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
                    table.PrimaryKey("PK_RestaurantOrderLineModifiers", x => x.RestaurantOrderLineModifierId);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLineModifiers_RestaurantOrderLines_Restauran~",
                        column: x => x.RestaurantOrderLineId,
                        principalSchema: "sal",
                        principalTable: "RestaurantOrderLines",
                        principalColumn: "RestaurantOrderLineId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_CustomerId_OrgId",
                schema: "sal",
                table: "AggregatorOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_OrgId_Aggregator_ExternalOrderId",
                schema: "sal",
                table: "AggregatorOrders",
                columns: new[] { "OrgId", "Aggregator", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_OrgId_ReceivedAt",
                schema: "sal",
                table: "AggregatorOrders",
                columns: new[] { "OrgId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AggregatorOrders_RestaurantOrderId",
                schema: "sal",
                table: "AggregatorOrders",
                column: "RestaurantOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_ApproverUserId",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_RoleId",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_Request",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "DocumentType", "RequestKind", "RequestId", "Round", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetailTaxes_Grain",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                columns: new[] { "CreditNoteDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNoteDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_InvoiceDetailId",
                schema: "sal",
                table: "CreditNoteDetails",
                column: "InvoiceDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_Item",
                schema: "sal",
                table: "CreditNoteDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_Line",
                schema: "sal",
                table: "CreditNoteDetails",
                columns: new[] { "CreditNoteId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Contact",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Date",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_InvoiceId",
                schema: "sal",
                table: "CreditNotes",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Number",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_OrgId_InvoiceId",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Status",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetailTaxes_Grain",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                columns: new[] { "DeliveryChallanDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_Item",
                schema: "sal",
                table: "DeliveryChallanDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_Line",
                schema: "sal",
                table: "DeliveryChallanDetails",
                columns: new[] { "DeliveryChallanId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_SalesOrderDetailId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                column: "SalesOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Contact",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Date",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Number",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_OrgId_DispatchDate",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "DispatchDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_SalesOrderId",
                schema: "sal",
                table: "DeliveryChallans",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_Status",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_CustomerId_OrgId",
                schema: "sal",
                table: "EInvoices",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_Irn",
                schema: "sal",
                table: "EInvoices",
                column: "Irn",
                unique: true,
                filter: "\"Irn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_OrgId_SourceType_SourceId",
                schema: "sal",
                table: "EInvoices",
                columns: new[] { "OrgId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_OrgId_Status_NextAttemptAt",
                schema: "sal",
                table: "EInvoices",
                columns: new[] { "OrgId", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_CustomerId_OrgId",
                schema: "sal",
                table: "EditLog",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_ChangedAt",
                schema: "sal",
                table: "EditLog",
                columns: new[] { "OrgId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_EntityName_ChangedAt",
                schema: "sal",
                table: "EditLog",
                columns: new[] { "OrgId", "EntityName", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_RootEntityName_RootEntityKey_ChangedAt",
                schema: "sal",
                table: "EditLog",
                columns: new[] { "OrgId", "RootEntityName", "RootEntityKey", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_UserId_ChangedAt",
                schema: "sal",
                table: "EditLog",
                columns: new[] { "OrgId", "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "sal",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "sal",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "sal",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EwayBills_CustomerId_OrgId",
                schema: "sal",
                table: "EwayBills",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EwayBills_EwbNo",
                schema: "sal",
                table: "EwayBills",
                column: "EwbNo",
                unique: true,
                filter: "\"EwbNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EwayBills_OrgId_SourceType_SourceId",
                schema: "sal",
                table: "EwayBills",
                columns: new[] { "OrgId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Floors_CustomerId_OrgId",
                schema: "sal",
                table: "Floors",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Floors_OrgId_Name",
                schema: "sal",
                table: "Floors",
                columns: new[] { "OrgId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetailTaxes_Grain",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                columns: new[] { "InvoiceDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_Item",
                schema: "sal",
                table: "InvoiceDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_Line",
                schema: "sal",
                table: "InvoiceDetails",
                columns: new[] { "InvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_SalesOrderDetailId",
                schema: "sal",
                table: "InvoiceDetails",
                column: "SalesOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceProjectItems_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceProjectItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceProjectItems_InvoiceId",
                schema: "sal",
                table: "InvoiceProjectItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceProjectItems_OrgId_InvoiceId_Kind_ItemId",
                schema: "sal",
                table: "InvoiceProjectItems",
                columns: new[] { "OrgId", "InvoiceId", "Kind", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTenders_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceTenders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTenders_InvoiceId",
                schema: "sal",
                table: "InvoiceTenders",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Contact",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId_OrgId",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Date",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DeliveryChallanId",
                schema: "sal",
                table: "Invoices",
                column: "DeliveryChallanId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Due",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_QuoteId",
                schema: "sal",
                table: "Invoices",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SalesOrderId",
                schema: "sal",
                table: "Invoices",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KotLines_CustomerId_OrgId",
                schema: "sal",
                table: "KotLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_KotLines_KotId",
                schema: "sal",
                table: "KotLines",
                column: "KotId");

            migrationBuilder.CreateIndex(
                name: "IX_KotLines_RestaurantOrderLineId",
                schema: "sal",
                table: "KotLines",
                column: "RestaurantOrderLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kots_CustomerId_OrgId",
                schema: "sal",
                table: "Kots",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Kots_OrgId_KotNo",
                schema: "sal",
                table: "Kots",
                columns: new[] { "OrgId", "KotNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kots_OrgId_Station_SentAt",
                schema: "sal",
                table: "Kots",
                columns: new[] { "OrgId", "Station", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Kots_RestaurantOrderId",
                schema: "sal",
                table: "Kots",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "QuoteDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetailTaxes_Grain",
                schema: "sal",
                table: "QuoteDetailTaxes",
                columns: new[] { "QuoteDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_CustomerId_OrgId",
                schema: "sal",
                table: "QuoteDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_Item",
                schema: "sal",
                table: "QuoteDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_Line",
                schema: "sal",
                table: "QuoteDetails",
                columns: new[] { "QuoteId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Contact",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId_OrgId",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Date",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Number",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Status",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringInvoiceRuns_CustomerId_OrgId",
                schema: "sal",
                table: "RecurringInvoiceRuns",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringInvoiceRuns_InvoiceId",
                schema: "sal",
                table: "RecurringInvoiceRuns",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringInvoiceRuns_OrgId_RecurringInvoiceScheduleId_Occur~",
                schema: "sal",
                table: "RecurringInvoiceRuns",
                columns: new[] { "OrgId", "RecurringInvoiceScheduleId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringInvoiceRuns_RecurringInvoiceScheduleId",
                schema: "sal",
                table: "RecurringInvoiceRuns",
                column: "RecurringInvoiceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringInvoiceSchedules_CustomerId_OrgId",
                schema: "sal",
                table: "RecurringInvoiceSchedules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringInvoiceSchedules_OrgId_Status_NextRunDate",
                schema: "sal",
                table: "RecurringInvoiceSchedules",
                columns: new[] { "OrgId", "Status", "NextRunDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringInvoiceSchedules_TemplateInvoiceId",
                schema: "sal",
                table: "RecurringInvoiceSchedules",
                column: "TemplateInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderLogs_CustomerId_OrgId",
                schema: "sal",
                table: "ReminderLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderProfiles_CustomerId_OrgId",
                schema: "sal",
                table: "ReminderProfiles",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantBills_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantBills",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantBills_InvoiceId",
                schema: "sal",
                table: "RestaurantBills",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantBills_OrgId_BilledAt",
                schema: "sal",
                table: "RestaurantBills",
                columns: new[] { "OrgId", "BilledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLineModifiers_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrderLineModifiers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLineModifiers_RestaurantOrderLineId",
                schema: "sal",
                table: "RestaurantOrderLineModifiers",
                column: "RestaurantOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrderLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_OrgId_KitchenStatus_Station",
                schema: "sal",
                table: "RestaurantOrderLines",
                columns: new[] { "OrgId", "KitchenStatus", "Station" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_RestaurantBillId",
                schema: "sal",
                table: "RestaurantOrderLines",
                column: "RestaurantBillId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_RestaurantOrderId",
                schema: "sal",
                table: "RestaurantOrderLines",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_MergedIntoOrderId",
                schema: "sal",
                table: "RestaurantOrders",
                column: "MergedIntoOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OrgId_OrderNo",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "OrgId", "OrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OrgId_RestaurantTableId",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "OrgId", "RestaurantTableId" },
                unique: true,
                filter: "\"Status\" = 'Open' AND \"RestaurantTableId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_OrgId_Status_OpenedAt",
                schema: "sal",
                table: "RestaurantOrders",
                columns: new[] { "OrgId", "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_RestaurantTableId",
                schema: "sal",
                table: "RestaurantOrders",
                column: "RestaurantTableId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_CustomerId_OrgId",
                schema: "sal",
                table: "RestaurantTables",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_FloorId",
                schema: "sal",
                table: "RestaurantTables",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_MergedIntoTableId",
                schema: "sal",
                table: "RestaurantTables",
                column: "MergedIntoTableId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_OrgId_Code",
                schema: "sal",
                table: "RestaurantTables",
                columns: new[] { "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_OrgId_FloorId_DisplayOrder",
                schema: "sal",
                table: "RestaurantTables",
                columns: new[] { "OrgId", "FloorId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetailTaxes_Grain",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                columns: new[] { "SalesOrderDetailId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrderDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_Item",
                schema: "sal",
                table: "SalesOrderDetails",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_Line",
                schema: "sal",
                table: "SalesOrderDetails",
                columns: new[] { "SalesOrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Contact",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "ContactId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Date",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Fulfilment",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "FulfilmentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Number",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_QuoteId",
                schema: "sal",
                table: "SalesOrders",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Status",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesRegister_CustomerId_OrgId",
                schema: "sal",
                table: "SalesRegister",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesRegister_TransactionTypeCode_SourceId",
                schema: "sal",
                table: "SalesRegister",
                columns: new[] { "TransactionTypeCode", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_CustomerId_OrgId",
                schema: "sal",
                table: "TableReservations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_OrgId_ReservedFor",
                schema: "sal",
                table: "TableReservations",
                columns: new[] { "OrgId", "ReservedFor" });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_OrgId_RestaurantTableId_Status_ReservedFor",
                schema: "sal",
                table: "TableReservations",
                columns: new[] { "OrgId", "RestaurantTableId", "Status", "ReservedFor" });

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_RestaurantOrderId",
                schema: "sal",
                table: "TableReservations",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_RestaurantTableId",
                schema: "sal",
                table: "TableReservations",
                column: "RestaurantTableId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_CustomerId_OrgId",
                schema: "sal",
                table: "WaitingListEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_OrgId_Status_JoinedAt",
                schema: "sal",
                table: "WaitingListEntries",
                columns: new[] { "OrgId", "Status", "JoinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_RestaurantOrderId",
                schema: "sal",
                table: "WaitingListEntries",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntries_RestaurantTableId",
                schema: "sal",
                table: "WaitingListEntries",
                column: "RestaurantTableId");
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

            Shared.Kernel.Audit.EditLogMigration.Up(migrationBuilder, "sal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Shared.Kernel.Audit.EditLogMigration.Down(migrationBuilder, "sal");

            migrationBuilder.DropTable(
                name: "AggregatorOrders",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "ApprovalSteps",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "CreditNoteDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "DeliveryChallanDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "EInvoices",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "EditLog",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "EwayBills",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "InvoiceDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "InvoiceProjectItems",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "InvoiceTenders",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "KotLines",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "QuoteDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RecurringInvoiceRuns",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "ReminderLogs",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "ReminderProfiles",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantOrderLineModifiers",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesOrderDetailTaxes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesRegister",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "TableReservations",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "WaitingListEntries",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "CreditNoteDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "DeliveryChallanDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Kots",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "QuoteDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RecurringInvoiceSchedules",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantOrderLines",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "CreditNotes",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "InvoiceDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantBills",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantOrders",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesOrderDetails",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RestaurantTables",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "DeliveryChallans",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Floors",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "SalesOrders",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "sal");
        }

        /// <summary>
        /// Every table guarded by the standard tenant policy, written out rather
        /// than derived at run time: a migration must do the same thing forever.
        /// Taken from the policies the old chains left in a migrated database.
        /// </summary>
        private static readonly (string Schema, string[] Tables)[] RlsTables =
        [
            ("sal",
            [
                "AggregatorOrders",
                "ApprovalSteps",
                "CreditNoteDetailTaxes",
                "CreditNoteDetails",
                "CreditNotes",
                "DeliveryChallanDetailTaxes",
                "DeliveryChallanDetails",
                "DeliveryChallans",
                "EInvoices",
                "ErrorLogs",
                "EwayBills",
                "Floors",
                "InvoiceDetailTaxes",
                "InvoiceDetails",
                "InvoiceProjectItems",
                "InvoiceTenders",
                "Invoices",
                "KotLines",
                "Kots",
                "QuoteDetailTaxes",
                "QuoteDetails",
                "Quotes",
                "RecurringInvoiceRuns",
                "RecurringInvoiceSchedules",
                "ReminderLogs",
                "ReminderProfiles",
                "RestaurantBills",
                "RestaurantOrderLineModifiers",
                "RestaurantOrderLines",
                "RestaurantOrders",
                "RestaurantTables",
                "SalesOrderDetailTaxes",
                "SalesOrderDetails",
                "SalesOrders",
                "SalesRegister",
                "TableReservations",
                "WaitingListEntries",
            ]),
        ];
    }
}
