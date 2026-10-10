using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Site.Repository.Migrations
{
    /// <inheritdoc />
    public partial class Migration_01_InitialSetup : Migration
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
                name: "MarginRules",
                schema: "sit",
                columns: table => new
                {
                    MarginRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<long>(type: "bigint", nullable: true),
                    AppliesTo = table.Column<int>(type: "integer", nullable: false),
                    MarginType = table.Column<int>(type: "integer", nullable: false),
                    MarginValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    GeofenceRadiusMeters = table.Column<int>(type: "integer", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EstimatedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    DefaultServiceItemId = table.Column<long>(type: "bigint", nullable: true),
                    DefaultCostRatePerDay = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DefaultSellRatePerDay = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DefaultOtCostRatePerHour = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DefaultOtSellRatePerHour = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "SiteStockBalances",
                schema: "sit",
                columns: table => new
                {
                    BalanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    DispatchedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReturnedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UsedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OnSiteQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnbilledUsedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BilledQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_SiteStockBalances", x => x.BalanceId);
                    table.ForeignKey(
                        name: "FK_SiteStockBalances_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "sit",
                        principalTable: "Sites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteStockDocuments",
                schema: "sit",
                columns: table => new
                {
                    DocumentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    SourceWarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_SiteStockDocuments", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_SiteStockDocuments_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "sit",
                        principalTable: "Sites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Laborers",
                schema: "sit",
                columns: table => new
                {
                    LaborerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradeId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CostRatePerDay = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SellRatePerDay = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    OtCostRatePerHour = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    OtSellRatePerHour = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_Laborers_Trades_TradeId",
                        column: x => x.TradeId,
                        principalSchema: "sit",
                        principalTable: "Trades",
                        principalColumn: "TradeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteStockLines",
                schema: "sit",
                columns: table => new
                {
                    LineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MarginValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BilledQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_SiteStockLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_SiteStockLines_SiteStockDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "sit",
                        principalTable: "SiteStockDocuments",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
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
                    TradeId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CostRatePerDay = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SellRatePerDay = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ApprovedDays = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    ApprovedOtHours = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    BilledDays = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    BilledOtHours = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_SiteAssignments_Laborers_LaborerId",
                        column: x => x.LaborerId,
                        principalSchema: "sit",
                        principalTable: "Laborers",
                        principalColumn: "LaborerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteAssignments_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "sit",
                        principalTable: "Sites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteAssignments_Trades_TradeId",
                        column: x => x.TradeId,
                        principalSchema: "sit",
                        principalTable: "Trades",
                        principalColumn: "TradeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteAttendance",
                schema: "sit",
                columns: table => new
                {
                    AttendanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignmentId = table.Column<long>(type: "bigint", nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    LaborerId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CheckInLat = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    CheckInLng = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    IsGeofenceBreached = table.Column<bool>(type: "boolean", nullable: false),
                    ClockOutTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DayFraction = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    OtHoursClaimed = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    OtHoursApproved = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    WorkSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DayCostSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DayMarginSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DaySellSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtCostSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtMarginSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtSellSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SiteBillLineId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_SiteAttendance", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_SiteAttendance_Laborers_LaborerId",
                        column: x => x.LaborerId,
                        principalSchema: "sit",
                        principalTable: "Laborers",
                        principalColumn: "LaborerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteAttendance_SiteAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "sit",
                        principalTable: "SiteAssignments",
                        principalColumn: "SiteAssignmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteAttendance_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "sit",
                        principalTable: "Sites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteAttendancePhotos",
                schema: "sit",
                columns: table => new
                {
                    PhotoId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceId = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_SiteAttendancePhotos", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_SiteAttendancePhotos_SiteAttendance_AttendanceId",
                        column: x => x.AttendanceId,
                        principalSchema: "sit",
                        principalTable: "SiteAttendance",
                        principalColumn: "AttendanceId",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_Laborers_TradeId",
                schema: "sit",
                table: "Laborers",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarginRules_CustomerId_OrgId",
                schema: "sit",
                table: "MarginRules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarginRules_CustomerId_OrgId_Scope_ScopeId",
                schema: "sit",
                table: "MarginRules",
                columns: new[] { "CustomerId", "OrgId", "Scope", "ScopeId" });

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
                name: "IX_SiteAssignments_LaborerId",
                schema: "sit",
                table: "SiteAssignments",
                column: "LaborerId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssignments_SiteId",
                schema: "sit",
                table: "SiteAssignments",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssignments_TradeId",
                schema: "sit",
                table: "SiteAssignments",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_AssignmentId",
                schema: "sit",
                table: "SiteAttendance",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_AssignmentId_WorkDate",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "AssignmentId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_LaborerId_WorkDate",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "LaborerId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_SiteId_WorkDate",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_Status",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_LaborerId",
                schema: "sit",
                table: "SiteAttendance",
                column: "LaborerId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_SiteId",
                schema: "sit",
                table: "SiteAttendance",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendancePhotos_AttendanceId",
                schema: "sit",
                table: "SiteAttendancePhotos",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendancePhotos_CustomerId_OrgId",
                schema: "sit",
                table: "SiteAttendancePhotos",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendancePhotos_CustomerId_OrgId_AttendanceId",
                schema: "sit",
                table: "SiteAttendancePhotos",
                columns: new[] { "CustomerId", "OrgId", "AttendanceId" });

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
                name: "IX_SiteStockBalances_CustomerId_OrgId",
                schema: "sit",
                table: "SiteStockBalances",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockBalances_CustomerId_OrgId_SiteId_ItemId",
                schema: "sit",
                table: "SiteStockBalances",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockBalances_SiteId",
                schema: "sit",
                table: "SiteStockBalances",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_CustomerId_OrgId",
                schema: "sit",
                table: "SiteStockDocuments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_CustomerId_OrgId_DocumentNo",
                schema: "sit",
                table: "SiteStockDocuments",
                columns: new[] { "CustomerId", "OrgId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_CustomerId_OrgId_SiteId_DocumentType",
                schema: "sit",
                table: "SiteStockDocuments",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockDocuments_SiteId",
                schema: "sit",
                table: "SiteStockDocuments",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockLines_CustomerId_OrgId",
                schema: "sit",
                table: "SiteStockLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockLines_CustomerId_OrgId_DocumentId",
                schema: "sit",
                table: "SiteStockLines",
                columns: new[] { "CustomerId", "OrgId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteStockLines_DocumentId",
                schema: "sit",
                table: "SiteStockLines",
                column: "DocumentId");

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

            Shared.Kernel.Audit.EditLogMigration.Up(migrationBuilder, "sit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Shared.Kernel.Audit.EditLogMigration.Down(migrationBuilder, "sit");

            migrationBuilder.DropTable(
                name: "EditLog",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "MarginRules",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteAttendancePhotos",
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
                name: "SiteStockBalances",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteStockLines",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteAttendance",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteStockDocuments",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteAssignments",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "Laborers",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "Sites",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "Trades",
                schema: "sit");
        }

        /// <summary>
        /// Every table guarded by the standard tenant policy, written out rather
        /// than derived at run time: a migration must do the same thing forever.
        /// Taken from the policies the old chains left in a migrated database.
        /// </summary>
        private static readonly (string Schema, string[] Tables)[] RlsTables =
        [
            ("sit",
            [
                "ErrorLogs",
                "Laborers",
                "MarginRules",
                "SiteAssignments",
                "SiteAttendance",
                "SiteAttendancePhotos",
                "SiteBillingInvoiceLines",
                "SiteBillingInvoices",
                "SiteDispatchLines",
                "SiteDispatches",
                "SiteMaterialUsageLines",
                "SiteMaterialUsages",
                "SiteReturnLines",
                "SiteReturns",
                "SiteStockBalances",
                "SiteStockDocuments",
                "SiteStockLines",
                "Sites",
                "Trades",
            ]),
        ];
    }
}
