using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_01_InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inv");

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "inv",
                columns: table => new
                {
                    InventoryApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Round = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ApprovalSteps", x => x.InventoryApprovalStepId);
                });

            migrationBuilder.CreateTable(
                name: "EditLog",
                schema: "inv",
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
                schema: "inv",
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
                name: "ItemCategories",
                schema: "inv",
                columns: table => new
                {
                    ItemCategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParentCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    DefaultItemProfile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    DefaultCostingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultUomTypeId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ItemCategories", x => x.ItemCategoryId);
                    table.ForeignKey(
                        name: "FK_ItemCategories_ItemCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalSchema: "inv",
                        principalTable: "ItemCategories",
                        principalColumn: "ItemCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuCategories",
                schema: "inv",
                columns: table => new
                {
                    MenuCategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_MenuCategories", x => x.MenuCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "MenuSchedules",
                schema: "inv",
                columns: table => new
                {
                    MenuScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
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
                    table.PrimaryKey("PK_MenuSchedules", x => x.MenuScheduleId);
                    table.CheckConstraint("chk_menu_schedule_days", "\"Days\" > 0 AND \"Days\" < 128");
                    table.CheckConstraint("chk_menu_schedule_times", "\"StartTime\" <> \"EndTime\"");
                });

            migrationBuilder.CreateTable(
                name: "MetalPurities",
                schema: "inv",
                columns: table => new
                {
                    MetalPurityId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetalType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PuritySystemName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PurityName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurityFactor = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_MetalPurities", x => x.MetalPurityId);
                    table.CheckConstraint("chk_purity_factor", "\"PurityFactor\" > 0 AND \"PurityFactor\" <= 1");
                });

            migrationBuilder.CreateTable(
                name: "PriceListItems",
                schema: "inv",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_PriceListItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceLists",
                schema: "inv",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UomTypes",
                schema: "inv",
                columns: table => new
                {
                    UomTypeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UomTypeSystemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    UomTypeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_UomTypes", x => x.UomTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                schema: "inv",
                columns: table => new
                {
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarehouseCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WarehouseName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WarehouseType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    StorageType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateId = table.Column<int>(type: "integer", nullable: true),
                    CountryId = table.Column<int>(type: "integer", nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ContactPersonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Warehouses", x => x.WarehouseId);
                });

            migrationBuilder.CreateTable(
                name: "UnitOfMeasures",
                schema: "inv",
                columns: table => new
                {
                    UomId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UomTypeId = table.Column<long>(type: "bigint", nullable: false),
                    UomSystemName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UomCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UqcCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UomName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsBaseUnit = table.Column<bool>(type: "boolean", nullable: false),
                    ConversionToBase = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_UnitOfMeasures", x => x.UomId);
                    table.CheckConstraint("chk_uom_base_factor", "\"IsBaseUnit\" = false OR \"ConversionToBase\" = 1");
                    table.CheckConstraint("chk_uom_conversion_positive", "\"ConversionToBase\" > 0");
                    table.CheckConstraint("chk_uom_decimals", "\"DecimalPlaces\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_UnitOfMeasures_UomTypes_UomTypeId",
                        column: x => x.UomTypeId,
                        principalSchema: "inv",
                        principalTable: "UomTypes",
                        principalColumn: "UomTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                schema: "inv",
                columns: table => new
                {
                    StockAdjustmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdjustmentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AdjustmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedByStockAdjustmentId = table.Column<long>(type: "bigint", nullable: true),
                    ReversesStockAdjustmentId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CurrentStepLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentApproverRoleId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_StockAdjustments", x => x.StockAdjustmentId);
                    table.CheckConstraint("chk_adjustment_not_self_reversing", "\"ReversesStockAdjustmentId\" IS NULL OR \"ReversesStockAdjustmentId\" <> \"StockAdjustmentId\"");
                    table.CheckConstraint("chk_adjustment_posted", "(\"Status\" = 'Draft' AND \"AdjustmentNo\" IS NULL AND \"PostedAt\" IS NULL) OR (\"Status\" <> 'Draft' AND \"AdjustmentNo\" IS NOT NULL AND \"PostedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StockAdjustments_StockAdjustments_ReversesStockAdjustmentId",
                        column: x => x.ReversesStockAdjustmentId,
                        principalSchema: "inv",
                        principalTable: "StockAdjustments",
                        principalColumn: "StockAdjustmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inv",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrintName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ItemProfile = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ItemCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    HsnSacCodeId = table.Column<int>(type: "integer", nullable: true),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: true),
                    TaxPreference = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IsPriceInclusiveOfTax = table.Column<bool>(type: "boolean", nullable: false),
                    UomTypeId = table.Column<long>(type: "bigint", nullable: false),
                    InventoryUomId = table.Column<long>(type: "bigint", nullable: false),
                    SalesUomId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseUomId = table.Column<long>(type: "bigint", nullable: false),
                    ReportUomId = table.Column<long>(type: "bigint", nullable: false),
                    TrackInventory = table.Column<bool>(type: "boolean", nullable: false),
                    CostingType = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    IsBatchTracked = table.Column<bool>(type: "boolean", nullable: false),
                    IsExpiryTracked = table.Column<bool>(type: "boolean", nullable: false),
                    IsSerialTracked = table.Column<bool>(type: "boolean", nullable: false),
                    SalesPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Mrp = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MinSalePrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    StandardCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    MinStockLevel = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    MaxStockLevel = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    DefaultWarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsReturnable = table.Column<bool>(type: "boolean", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CustomFields = table.Column<JsonDocument>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_Items", x => x.ItemId);
                    table.CheckConstraint("chk_item_costing_tracking", "(\"TrackInventory\" = false AND \"CostingType\" = 'None') OR (\"TrackInventory\" = true AND \"CostingType\" <> 'None')");
                    table.CheckConstraint("chk_item_expiry_needs_batch", "\"IsExpiryTracked\" = false OR \"IsBatchTracked\" = true");
                    table.CheckConstraint("chk_item_fefo_tracking", "\"CostingType\" <> 'Fefo' OR (\"IsBatchTracked\" = true AND \"IsExpiryTracked\" = true)");
                    table.CheckConstraint("chk_item_min_sale_price", "\"MinSalePrice\" IS NULL OR \"SalesPrice\" IS NULL OR \"MinSalePrice\" <= \"SalesPrice\"");
                    table.CheckConstraint("chk_item_specific_tracking", "\"CostingType\" <> 'SpecificIdentification' OR \"IsSerialTracked\" = true");
                    table.ForeignKey(
                        name: "FK_Items_ItemCategories_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalSchema: "inv",
                        principalTable: "ItemCategories",
                        principalColumn: "ItemCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Items_UnitOfMeasures_InventoryUomId",
                        column: x => x.InventoryUomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Items_UomTypes_UomTypeId",
                        column: x => x.UomTypeId,
                        principalSchema: "inv",
                        principalTable: "UomTypes",
                        principalColumn: "UomTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemBarcodes",
                schema: "inv",
                columns: table => new
                {
                    ItemBarcodeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BarcodeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ItemBarcodes", x => x.ItemBarcodeId);
                    table.ForeignKey(
                        name: "FK_ItemBarcodes_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemBatches",
                schema: "inv",
                columns: table => new
                {
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Mrp = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SupplierBatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_ItemBatches", x => x.ItemBatchId);
                    table.CheckConstraint("chk_batch_dates", "\"ManufactureDate\" IS NULL OR \"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"ManufactureDate\"");
                    table.ForeignKey(
                        name: "FK_ItemBatches_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemJewelleryDetails",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    MetalType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MetalPurityId = table.Column<long>(type: "bigint", nullable: false),
                    GrossWeight = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    NetWeight = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    StoneWeight = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    StoneCharge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WastagePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MakingChargeType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    MakingChargeValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsHallmarked = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ItemJewelleryDetails", x => x.ItemId);
                    table.CheckConstraint("chk_jewellery_making_percent", "\"MakingChargeType\" <> 'Percentage' OR \"MakingChargeValue\" <= 100");
                    table.CheckConstraint("chk_jewellery_weights", "\"NetWeight\" <= \"GrossWeight\" AND \"StoneWeight\" <= \"GrossWeight\"");
                    table.ForeignKey(
                        name: "FK_ItemJewelleryDetails_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemJewelleryDetails_MetalPurities_MetalPurityId",
                        column: x => x.MetalPurityId,
                        principalSchema: "inv",
                        principalTable: "MetalPurities",
                        principalColumn: "MetalPurityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemPharmaDetails",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    GenericName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DosageForm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PackSize = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufacturerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MarketedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DrugSchedule = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsPrescriptionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsNarcotic = table.Column<bool>(type: "boolean", nullable: false),
                    StorageCondition = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    ShelfLifeDays = table.Column<int>(type: "integer", nullable: true),
                    MinExpiryDaysOnReceipt = table.Column<int>(type: "integer", nullable: false),
                    ExpiryAlertDays = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ItemPharmaDetails", x => x.ItemId);
                    table.CheckConstraint("chk_pharma_prescription", "\"DrugSchedule\" NOT IN ('H', 'H1', 'X') OR \"IsPrescriptionRequired\" = true");
                    table.ForeignKey(
                        name: "FK_ItemPharmaDetails_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemStock",
                schema: "inv",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    WeightedAverageCost = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                    QuantityReserved = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    LastMovementAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ItemStock", x => x.ItemId);
                    table.CheckConstraint("chk_item_stock_non_negative", "\"QuantityOnHand\" >= 0 AND \"WeightedAverageCost\" >= 0");
                    table.CheckConstraint("chk_stock_reserved", "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");
                    table.ForeignKey(
                        name: "FK_ItemStock_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                schema: "inv",
                columns: table => new
                {
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    MenuCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    MenuScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    Station = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_MenuItems", x => x.MenuItemId);
                    table.ForeignKey(
                        name: "FK_MenuItems_Items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuCategories_MenuCategoryId",
                        column: x => x.MenuCategoryId,
                        principalSchema: "inv",
                        principalTable: "MenuCategories",
                        principalColumn: "MenuCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuSchedules_MenuScheduleId",
                        column: x => x.MenuScheduleId,
                        principalSchema: "inv",
                        principalTable: "MenuSchedules",
                        principalColumn: "MenuScheduleId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WastageEntries",
                schema: "inv",
                columns: table => new
                {
                    WastageEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    StockAdjustmentId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_WastageEntries", x => x.WastageEntryId);
                    table.CheckConstraint("chk_wastage_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_WastageEntries_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WastageEntries_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalSchema: "inv",
                        principalTable: "StockAdjustments",
                        principalColumn: "StockAdjustmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WastageEntries_UnitOfMeasures_UomId",
                        column: x => x.UomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemSerials",
                schema: "inv",
                columns: table => new
                {
                    ItemSerialId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    HallmarkNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    CostLayerId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    ReceivedMovementId = table.Column<long>(type: "bigint", nullable: true),
                    IssuedMovementId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ItemSerials", x => x.ItemSerialId);
                    table.ForeignKey(
                        name: "FK_ItemSerials_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalSchema: "inv",
                        principalTable: "ItemBatches",
                        principalColumn: "ItemBatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemSerials_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "inv",
                columns: table => new
                {
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    MovementType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Direction = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EnteredQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    EnteredUomId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(28,12)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ResultingWeightedAverageCost = table.Column<decimal>(type: "numeric(28,12)", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    SourceLineId = table.Column<long>(type: "bigint", nullable: false),
                    CostingStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CostingAttempts = table.Column<int>(type: "integer", nullable: false),
                    CostingError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LedgerStatus = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    LedgerPostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LedgerAttempts = table.Column<int>(type: "integer", nullable: false),
                    LedgerError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LedgerExempt = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnsStockMovementId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_StockMovements", x => x.StockMovementId);
                    table.CheckConstraint("chk_movement_conversion", "\"Quantity\" = ROUND(\"EnteredQuantity\" * \"ConversionFactor\", 3)");
                    table.CheckConstraint("chk_movement_cost_non_negative", "(\"UnitCost\" IS NULL OR \"UnitCost\" >= 0) AND (\"TotalCost\" IS NULL OR \"TotalCost\" >= 0)");
                    table.CheckConstraint("chk_movement_quantity_positive", "\"Quantity\" > 0 AND \"EnteredQuantity\" > 0 AND \"ConversionFactor\" > 0");
                    table.CheckConstraint("chk_movement_source", "(\"SourceType\" IS NULL AND \"SourceId\" IS NULL) OR (\"SourceType\" IS NOT NULL AND \"SourceId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StockMovements_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalSchema: "inv",
                        principalTable: "ItemBatches",
                        principalColumn: "ItemBatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_UnitOfMeasures_EnteredUomId",
                        column: x => x.EnteredUomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inv",
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemVariants",
                schema: "inv",
                columns: table => new
                {
                    ItemVariantId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PortionFactor = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ItemVariants", x => x.ItemVariantId);
                    table.CheckConstraint("chk_item_variant_portion", "\"PortionFactor\" > 0");
                    table.CheckConstraint("chk_item_variant_price", "\"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_ItemVariants_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModifierGroups",
                schema: "inv",
                columns: table => new
                {
                    ModifierGroupId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinChoices = table.Column<int>(type: "integer", nullable: false),
                    MaxChoices = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ModifierGroups", x => x.ModifierGroupId);
                    table.CheckConstraint("chk_modifier_group_choices", "\"MinChoices\" >= 0 AND \"MaxChoices\" >= 1 AND \"MinChoices\" <= \"MaxChoices\"");
                    table.ForeignKey(
                        name: "FK_ModifierGroups_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                schema: "inv",
                columns: table => new
                {
                    RecipeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Yield = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("PK_Recipes", x => x.RecipeId);
                    table.CheckConstraint("chk_recipe_yield", "\"Yield\" > 0");
                    table.ForeignKey(
                        name: "FK_Recipes_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostLayers",
                schema: "inv",
                columns: table => new
                {
                    CostLayerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    ItemBatchId = table.Column<long>(type: "bigint", nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
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
                    table.PrimaryKey("PK_CostLayers", x => x.CostLayerId);
                    table.CheckConstraint("chk_layer_remaining", "\"RemainingQuantity\" >= 0 AND \"RemainingQuantity\" <= \"OriginalQuantity\" AND \"OriginalQuantity\" > 0 AND \"UnitCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_CostLayers_ItemBatches_ItemBatchId",
                        column: x => x.ItemBatchId,
                        principalSchema: "inv",
                        principalTable: "ItemBatches",
                        principalColumn: "ItemBatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostLayers_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostLayers_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecostingAdjustments",
                schema: "inv",
                columns: table => new
                {
                    RecostingAdjustmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecostingBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    TriggerStockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NewCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_RecostingAdjustments", x => x.RecostingAdjustmentId);
                    table.CheckConstraint("chk_recosting_delta", "\"Delta\" = \"NewCost\" - \"PreviousCost\"");
                    table.ForeignKey(
                        name: "FK_RecostingAdjustments_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecostingAdjustments_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentLines",
                schema: "inv",
                columns: table => new
                {
                    StockAdjustmentLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockAdjustmentId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: true),
                    SystemQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Direction = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BatchExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_StockAdjustmentLines", x => x.StockAdjustmentLineId);
                    table.CheckConstraint("chk_adjustment_line_cost", "\"UnitCost\" IS NULL OR \"Direction\" = 'In'");
                    table.CheckConstraint("chk_adjustment_line_count", "(\"CountedQuantity\" IS NULL AND \"SystemQuantity\" IS NULL) OR (\"CountedQuantity\" IS NOT NULL AND \"SystemQuantity\" IS NOT NULL)");
                    table.CheckConstraint("chk_adjustment_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalSchema: "inv",
                        principalTable: "StockAdjustments",
                        principalColumn: "StockAdjustmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemPrices",
                schema: "inv",
                columns: table => new
                {
                    MenuItemPriceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemVariantId = table.Column<long>(type: "bigint", nullable: true),
                    OrderType = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_MenuItemPrices", x => x.MenuItemPriceId);
                    table.CheckConstraint("chk_menu_item_price", "\"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_MenuItemPrices_ItemVariants_ItemVariantId",
                        column: x => x.ItemVariantId,
                        principalSchema: "inv",
                        principalTable: "ItemVariants",
                        principalColumn: "ItemVariantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemPrices_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "inv",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Modifiers",
                schema: "inv",
                columns: table => new
                {
                    ModifierId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModifierGroupId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceChange = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LinkedItemId = table.Column<long>(type: "bigint", nullable: true),
                    LinkedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Modifiers", x => x.ModifierId);
                    table.CheckConstraint("chk_modifier_linked_quantity", "(\"LinkedItemId\" IS NULL) = (\"LinkedQuantity\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Modifiers_Items_LinkedItemId",
                        column: x => x.LinkedItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Modifiers_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalSchema: "inv",
                        principalTable: "ModifierGroups",
                        principalColumn: "ModifierGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeLines",
                schema: "inv",
                columns: table => new
                {
                    RecipeLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<long>(type: "bigint", nullable: false),
                    IngredientItemId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UomId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RecipeLines", x => x.RecipeLineId);
                    table.CheckConstraint("chk_recipe_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_RecipeLines_Items_IngredientItemId",
                        column: x => x.IngredientItemId,
                        principalSchema: "inv",
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeLines_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalSchema: "inv",
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeLines_UnitOfMeasures_UomId",
                        column: x => x.UomId,
                        principalSchema: "inv",
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostLayerConsumptions",
                schema: "inv",
                columns: table => new
                {
                    CostLayerConsumptionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostLayerId = table.Column<long>(type: "bigint", nullable: false),
                    StockMovementId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecostingBatchId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_CostLayerConsumptions", x => x.CostLayerConsumptionId);
                    table.CheckConstraint("chk_consumption_amounts", "\"Quantity\" > 0 AND \"UnitCost\" >= 0 AND \"TotalCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_CostLayerConsumptions_CostLayers_CostLayerId",
                        column: x => x.CostLayerId,
                        principalSchema: "inv",
                        principalTable: "CostLayers",
                        principalColumn: "CostLayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostLayerConsumptions_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "inv",
                        principalTable: "StockMovements",
                        principalColumn: "StockMovementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_RequestKind_RequestId_Round_Sequence",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "RequestKind", "RequestId", "Round", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_ApproverUserId",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_RoleId",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Allocation",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "StockMovementId", "CostLayerId" },
                unique: true,
                filter: "\"SupersededAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_CostLayerId",
                schema: "inv",
                table: "CostLayerConsumptions",
                column: "CostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_CustomerId_OrgId",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Layer",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "CostLayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_Movement",
                schema: "inv",
                table: "CostLayerConsumptions",
                columns: new[] { "OrgId", "StockMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayerConsumptions_StockMovementId",
                schema: "inv",
                table: "CostLayerConsumptions",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_CustomerId_OrgId",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_Fefo",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "OrgId", "ItemId", "ExpiresOn" },
                filter: "\"RemainingQuantity\" > 0 AND \"ExpiresOn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_Fifo",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "OrgId", "ItemId", "ReceivedOn" },
                filter: "\"RemainingQuantity\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_ItemBatchId",
                schema: "inv",
                table: "CostLayers",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_ItemId",
                schema: "inv",
                table: "CostLayers",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_OrgId_StockMovementId",
                schema: "inv",
                table: "CostLayers",
                columns: new[] { "OrgId", "StockMovementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_StockMovementId",
                schema: "inv",
                table: "CostLayers",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_CustomerId_OrgId",
                schema: "inv",
                table: "EditLog",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_ChangedAt",
                schema: "inv",
                table: "EditLog",
                columns: new[] { "OrgId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_EntityName_ChangedAt",
                schema: "inv",
                table: "EditLog",
                columns: new[] { "OrgId", "EntityName", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_RootEntityName_RootEntityKey_ChangedAt",
                schema: "inv",
                table: "EditLog",
                columns: new[] { "OrgId", "RootEntityName", "RootEntityKey", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_UserId_ChangedAt",
                schema: "inv",
                table: "EditLog",
                columns: new[] { "OrgId", "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "inv",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "inv",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "inv",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_CustomerId_OrgId",
                schema: "inv",
                table: "ItemBarcodes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_ItemId",
                schema: "inv",
                table: "ItemBarcodes",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_OrgId_Barcode",
                schema: "inv",
                table: "ItemBarcodes",
                columns: new[] { "OrgId", "Barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemBarcodes_Primary",
                schema: "inv",
                table: "ItemBarcodes",
                columns: new[] { "OrgId", "ItemId" },
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_CustomerId_OrgId",
                schema: "inv",
                table: "ItemBatches",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_Expiry",
                schema: "inv",
                table: "ItemBatches",
                columns: new[] { "OrgId", "ItemId", "ExpiryDate" },
                filter: "\"ExpiryDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ItemId",
                schema: "inv",
                table: "ItemBatches",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_OrgId_ItemId_BatchNumber",
                schema: "inv",
                table: "ItemBatches",
                columns: new[] { "OrgId", "ItemId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_CustomerId_OrgId",
                schema: "inv",
                table: "ItemCategories",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_Order",
                schema: "inv",
                table: "ItemCategories",
                columns: new[] { "OrgId", "ParentCategoryId", "DisplayOrder", "CategoryName" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_OrgId_CategoryCode",
                schema: "inv",
                table: "ItemCategories",
                columns: new[] { "OrgId", "CategoryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_ParentCategoryId",
                schema: "inv",
                table: "ItemCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemJewelleryDetails_CustomerId_OrgId",
                schema: "inv",
                table: "ItemJewelleryDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemJewelleryDetails_MetalPurityId",
                schema: "inv",
                table: "ItemJewelleryDetails",
                column: "MetalPurityId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPharmaDetails_CustomerId_OrgId",
                schema: "inv",
                table: "ItemPharmaDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemPharmaDetails_Generic",
                schema: "inv",
                table: "ItemPharmaDetails",
                columns: new[] { "OrgId", "GenericName" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_CustomerId_OrgId",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_Huid",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "OrgId", "HallmarkNumber" },
                unique: true,
                filter: "\"HallmarkNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_ItemBatchId",
                schema: "inv",
                table: "ItemSerials",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_ItemId",
                schema: "inv",
                table: "ItemSerials",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_OrgId_ItemId_SerialNumber",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "OrgId", "ItemId", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemSerials_Status",
                schema: "inv",
                table: "ItemSerials",
                columns: new[] { "OrgId", "ItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemStock_CustomerId_OrgId",
                schema: "inv",
                table: "ItemStock",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_CustomerId_OrgId",
                schema: "inv",
                table: "ItemVariants",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_MenuItemId_Name",
                schema: "inv",
                table: "ItemVariants",
                columns: new[] { "MenuItemId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_CustomFields",
                schema: "inv",
                table: "Items",
                column: "CustomFields")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CustomerId_OrgId",
                schema: "inv",
                table: "Items",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_InventoryUomId",
                schema: "inv",
                table: "Items",
                column: "InventoryUomId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemCategoryId",
                schema: "inv",
                table: "Items",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Order",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "DisplayOrder", "ItemName" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_HsnSacCodeId",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "HsnSacCodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_ItemCategoryId",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "ItemCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_ItemCode",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_ItemName",
                schema: "inv",
                table: "Items",
                columns: new[] { "OrgId", "ItemName" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_Purchase",
                schema: "inv",
                table: "Items",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Sales",
                schema: "inv",
                table: "Items",
                column: "OrgId",
                filter: "\"IsSales\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Items_UomTypeId",
                schema: "inv",
                table: "Items",
                column: "UomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_CustomerId_OrgId",
                schema: "inv",
                table: "MenuCategories",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_OrgId_DisplayOrder",
                schema: "inv",
                table: "MenuCategories",
                columns: new[] { "OrgId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_OrgId_Name",
                schema: "inv",
                table: "MenuCategories",
                columns: new[] { "OrgId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPrices_CustomerId_OrgId",
                schema: "inv",
                table: "MenuItemPrices",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPrices_ItemVariantId",
                schema: "inv",
                table: "MenuItemPrices",
                column: "ItemVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPrices_MenuItemId_ItemVariantId_OrderType",
                schema: "inv",
                table: "MenuItemPrices",
                columns: new[] { "MenuItemId", "ItemVariantId", "OrderType" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CustomerId_OrgId",
                schema: "inv",
                table: "MenuItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuCategoryId",
                schema: "inv",
                table: "MenuItems",
                column: "MenuCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuScheduleId",
                schema: "inv",
                table: "MenuItems",
                column: "MenuScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_OrgId_MenuCategoryId_DisplayOrder",
                schema: "inv",
                table: "MenuItems",
                columns: new[] { "OrgId", "MenuCategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSchedules_CustomerId_OrgId",
                schema: "inv",
                table: "MenuSchedules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuSchedules_OrgId_Name",
                schema: "inv",
                table: "MenuSchedules",
                columns: new[] { "OrgId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_CustomerId_OrgId",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_Order",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "OrgId", "MetalType", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_OrgId_MetalType_PurityName",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "OrgId", "MetalType", "PurityName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalPurities_SystemName",
                schema: "inv",
                table: "MetalPurities",
                columns: new[] { "OrgId", "PuritySystemName" },
                unique: true,
                filter: "\"PuritySystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_CustomerId_OrgId",
                schema: "inv",
                table: "ModifierGroups",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_MenuItemId_Name",
                schema: "inv",
                table: "ModifierGroups",
                columns: new[] { "MenuItemId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modifiers_CustomerId_OrgId",
                schema: "inv",
                table: "Modifiers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Modifiers_LinkedItemId",
                schema: "inv",
                table: "Modifiers",
                column: "LinkedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifiers_ModifierGroupId_Name",
                schema: "inv",
                table: "Modifiers",
                columns: new[] { "ModifierGroupId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListItems_CustomerId_OrgId",
                schema: "inv",
                table: "PriceListItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_CustomerId_OrgId",
                schema: "inv",
                table: "PriceLists",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_CustomerId_OrgId",
                schema: "inv",
                table: "RecipeLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_IngredientItemId",
                schema: "inv",
                table: "RecipeLines",
                column: "IngredientItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_RecipeId_IngredientItemId",
                schema: "inv",
                table: "RecipeLines",
                columns: new[] { "RecipeId", "IngredientItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_UomId",
                schema: "inv",
                table: "RecipeLines",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CustomerId_OrgId",
                schema: "inv",
                table: "Recipes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_MenuItemId",
                schema: "inv",
                table: "Recipes",
                column: "MenuItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_Batch",
                schema: "inv",
                table: "RecostingAdjustments",
                columns: new[] { "OrgId", "RecostingBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_CustomerId_OrgId",
                schema: "inv",
                table: "RecostingAdjustments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_Item",
                schema: "inv",
                table: "RecostingAdjustments",
                columns: new[] { "OrgId", "ItemId", "RunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_ItemId",
                schema: "inv",
                table: "RecostingAdjustments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_Movement",
                schema: "inv",
                table: "RecostingAdjustments",
                columns: new[] { "OrgId", "StockMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecostingAdjustments_StockMovementId",
                schema: "inv",
                table: "RecostingAdjustments",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_CustomerId_OrgId",
                schema: "inv",
                table: "StockAdjustmentLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_Item",
                schema: "inv",
                table: "StockAdjustmentLines",
                columns: new[] { "OrgId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_ItemId",
                schema: "inv",
                table: "StockAdjustmentLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_Line",
                schema: "inv",
                table: "StockAdjustmentLines",
                columns: new[] { "StockAdjustmentId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_StockMovementId",
                schema: "inv",
                table: "StockAdjustmentLines",
                column: "StockMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_CustomerId_OrgId",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Date",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "OrgId", "AdjustmentDate", "StockAdjustmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Number",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "OrgId", "AdjustmentNo" },
                unique: true,
                filter: "\"AdjustmentNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ReversesStockAdjustmentId",
                schema: "inv",
                table: "StockAdjustments",
                column: "ReversesStockAdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Status",
                schema: "inv",
                table: "StockAdjustments",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_WarehouseId",
                schema: "inv",
                table: "StockAdjustments",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CostingQueue",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "ItemId", "MovementDate", "StockMovementId" },
                filter: "\"CostingStatus\" IN ('Pending', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CustomerId_OrgId",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Date",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_EnteredUomId",
                schema: "inv",
                table: "StockMovements",
                column: "EnteredUomId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Item",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "ItemId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemBatchId",
                schema: "inv",
                table: "StockMovements",
                column: "ItemBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId",
                schema: "inv",
                table: "StockMovements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LedgerQueue",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "MovementDate", "StockMovementId" },
                filter: "\"LedgerStatus\" IN ('Pending', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Source",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "SourceType", "SourceId", "SourceLineId" },
                unique: true,
                filter: "\"SourceType\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Warehouse",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "WarehouseId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId",
                schema: "inv",
                table: "StockMovements",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_BaseUnit",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomTypeId" },
                unique: true,
                filter: "\"IsBaseUnit\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_CustomerId_OrgId",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_Order",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomTypeId", "DisplayOrder", "UomCode" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_OrgId_UomCode",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_SystemName",
                schema: "inv",
                table: "UnitOfMeasures",
                columns: new[] { "OrgId", "UomSystemName" },
                unique: true,
                filter: "\"UomSystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_UomTypeId",
                schema: "inv",
                table: "UnitOfMeasures",
                column: "UomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_CustomerId_OrgId",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_Order",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "OrgId", "DisplayOrder", "UomTypeName" });

            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_OrgId_UomTypeName",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "OrgId", "UomTypeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UomTypes_SystemName",
                schema: "inv",
                table: "UomTypes",
                columns: new[] { "OrgId", "UomTypeSystemName" },
                unique: true,
                filter: "\"UomTypeSystemName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CustomerId_OrgId",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Default",
                schema: "inv",
                table: "Warehouses",
                column: "OrgId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Order",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "OrgId", "DisplayOrder", "WarehouseName" });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_OrgId_WarehouseCode",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "OrgId", "WarehouseCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_SiteId",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "OrgId", "SiteId" },
                filter: "\"SiteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_CustomerId_OrgId",
                schema: "inv",
                table: "WastageEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_ItemId",
                schema: "inv",
                table: "WastageEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_OrgId_EntryDate",
                schema: "inv",
                table: "WastageEntries",
                columns: new[] { "OrgId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_StockAdjustmentId",
                schema: "inv",
                table: "WastageEntries",
                column: "StockAdjustmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WastageEntries_UomId",
                schema: "inv",
                table: "WastageEntries",
                column: "UomId");
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

            Shared.Kernel.Audit.EditLogMigration.Up(migrationBuilder, "inv");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Shared.Kernel.Audit.EditLogMigration.Down(migrationBuilder, "inv");

            migrationBuilder.DropTable(
                name: "ApprovalSteps",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "CostLayerConsumptions",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "EditLog",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemBarcodes",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemJewelleryDetails",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemPharmaDetails",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemSerials",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemStock",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MenuItemPrices",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "Modifiers",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "PriceListItems",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "PriceLists",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "RecipeLines",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "RecostingAdjustments",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "StockAdjustmentLines",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "WastageEntries",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "CostLayers",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MetalPurities",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemVariants",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ModifierGroups",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "Recipes",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "StockAdjustments",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MenuItems",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemBatches",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "Warehouses",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MenuCategories",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "MenuSchedules",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "ItemCategories",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "UnitOfMeasures",
                schema: "inv");

            migrationBuilder.DropTable(
                name: "UomTypes",
                schema: "inv");
        }

        /// <summary>
        /// Every table guarded by the standard tenant policy, written out rather
        /// than derived at run time: a migration must do the same thing forever.
        /// Taken from the policies the old chains left in a migrated database.
        /// </summary>
        private static readonly (string Schema, string[] Tables)[] RlsTables =
        [
            ("inv",
            [
                "ApprovalSteps",
                "CostLayerConsumptions",
                "CostLayers",
                "ErrorLogs",
                "ItemBarcodes",
                "ItemBatches",
                "ItemCategories",
                "ItemJewelleryDetails",
                "ItemPharmaDetails",
                "ItemSerials",
                "ItemStock",
                "ItemVariants",
                "Items",
                "MenuCategories",
                "MenuItemPrices",
                "MenuItems",
                "MenuSchedules",
                "MetalPurities",
                "ModifierGroups",
                "Modifiers",
                "PriceListItems",
                "PriceLists",
                "RecipeLines",
                "Recipes",
                "RecostingAdjustments",
                "StockAdjustmentLines",
                "StockAdjustments",
                "StockMovements",
                "UnitOfMeasures",
                "UomTypes",
                "Warehouses",
                "WastageEntries",
            ]),
        ];
    }
}
