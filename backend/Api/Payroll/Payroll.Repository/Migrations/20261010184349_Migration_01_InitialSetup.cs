using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Payroll.Repository.Migrations
{
    /// <inheritdoc />
    public partial class Migration_01_InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pay");

            migrationBuilder.CreateTable(
                name: "BonusSettings",
                schema: "pay",
                columns: table => new
                {
                    BonusSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MinBonusPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxBonusPercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    CalculationCeiling = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_BonusSettings", x => x.BonusSettingId);
                });

            migrationBuilder.CreateTable(
                name: "EditLog",
                schema: "pay",
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
                name: "EmployeeLoans",
                schema: "pay",
                columns: table => new
                {
                    EmployeeLoanId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: false),
                    MonthlyEmi = table.Column<decimal>(type: "numeric", nullable: false),
                    DisbursedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeductionStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsSettled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_EmployeeLoans", x => x.EmployeeLoanId);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "pay",
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
                name: "EsiSettings",
                schema: "pay",
                columns: table => new
                {
                    EsiSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    WageCeiling = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_EsiSettings", x => x.EsiSettingId);
                });

            migrationBuilder.CreateTable(
                name: "FullAndFinalSettlements",
                schema: "pay",
                columns: table => new
                {
                    FullAndFinalSettlementId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    SeparationId = table.Column<long>(type: "bigint", nullable: true),
                    LastWorkingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayrollRunId = table.Column<long>(type: "bigint", nullable: true),
                    NetPayable = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_FullAndFinalSettlements", x => x.FullAndFinalSettlementId);
                });

            migrationBuilder.CreateTable(
                name: "GratuitySettings",
                schema: "pay",
                columns: table => new
                {
                    GratuitySettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    GratuityPercentage = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_GratuitySettings", x => x.GratuitySettingId);
                });

            migrationBuilder.CreateTable(
                name: "LwfSettings",
                schema: "pay",
                columns: table => new
                {
                    LwfSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeContribution = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerContribution = table.Column<decimal>(type: "numeric", nullable: false),
                    DeductionFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_LwfSettings", x => x.LwfSettingId);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyAttendanceInputs",
                schema: "pay",
                columns: table => new
                {
                    MonthlyAttendanceInputId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidDays = table.Column<decimal>(type: "numeric", nullable: false),
                    LossOfPayDays = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_MonthlyAttendanceInputs", x => x.MonthlyAttendanceInputId);
                });

            migrationBuilder.CreateTable(
                name: "PayGroups",
                schema: "pay",
                columns: table => new
                {
                    PayGroupId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_PayGroups", x => x.PayGroupId);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                schema: "pay",
                columns: table => new
                {
                    PayrollRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    TotalNetPay = table.Column<decimal>(type: "numeric", nullable: false),
                    DaysSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    JournalId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_PayrollRuns", x => x.PayrollRunId);
                });

            migrationBuilder.CreateTable(
                name: "PfSettings",
                schema: "pay",
                columns: table => new
                {
                    PfSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployeeContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerContributionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    WageCeiling = table.Column<decimal>(type: "numeric", nullable: false),
                    RestrictToWageCeiling = table.Column<bool>(type: "boolean", nullable: false),
                    AdminChargesRate = table.Column<decimal>(type: "numeric", nullable: false),
                    EdliRate = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_PfSettings", x => x.PfSettingId);
                });

            migrationBuilder.CreateTable(
                name: "PreviousEmployerIncomes",
                schema: "pay",
                columns: table => new
                {
                    PreviousEmployerIncomeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GrossIncome = table.Column<decimal>(type: "numeric", nullable: false),
                    Exemptions = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfessionalTax = table.Column<decimal>(type: "numeric", nullable: false),
                    ProvidentFund = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTdsDeducted = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_PreviousEmployerIncomes", x => x.PreviousEmployerIncomeId);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalTaxSlabs",
                schema: "pay",
                columns: table => new
                {
                    ProfessionalTaxSlabId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MinSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    MonthlyTax = table.Column<decimal>(type: "numeric", nullable: false),
                    Month12Tax = table.Column<decimal>(type: "numeric", nullable: true),
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
                    table.PrimaryKey("PK_ProfessionalTaxSlabs", x => x.ProfessionalTaxSlabId);
                });

            migrationBuilder.CreateTable(
                name: "SalaryComponents",
                schema: "pay",
                columns: table => new
                {
                    SalaryComponentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                    Formula = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LedgerAccountId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_SalaryComponents", x => x.SalaryComponentId);
                });

            migrationBuilder.CreateTable(
                name: "SalaryHolds",
                schema: "pay",
                columns: table => new
                {
                    SalaryHoldId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    FromMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    ToMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_SalaryHolds", x => x.SalaryHoldId);
                });

            migrationBuilder.CreateTable(
                name: "SalaryStructures",
                schema: "pay",
                columns: table => new
                {
                    SalaryStructureId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_SalaryStructures", x => x.SalaryStructureId);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryReturns",
                schema: "pay",
                columns: table => new
                {
                    StatutoryReturnId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileContent = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_StatutoryReturns", x => x.StatutoryReturnId);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarations",
                schema: "pay",
                columns: table => new
                {
                    TaxDeclarationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TaxDeclarations", x => x.TaxDeclarationId);
                });

            migrationBuilder.CreateTable(
                name: "TaxRules",
                schema: "pay",
                columns: table => new
                {
                    TaxRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Section = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxLimit = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_TaxRules", x => x.TaxRuleId);
                });

            migrationBuilder.CreateTable(
                name: "TaxSlabs",
                schema: "pay",
                columns: table => new
                {
                    TaxSlabId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FinancialYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Regime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MinIncome = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxIncome = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric", nullable: false),
                    CessRate = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_TaxSlabs", x => x.TaxSlabId);
                });

            migrationBuilder.CreateTable(
                name: "LoanRepayments",
                schema: "pay",
                columns: table => new
                {
                    LoanRepaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeLoanId = table.Column<long>(type: "bigint", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    PrincipalComponent = table.Column<decimal>(type: "numeric", nullable: false),
                    InterestComponent = table.Column<decimal>(type: "numeric", nullable: false),
                    Processed = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LoanRepayments", x => x.LoanRepaymentId);
                    table.ForeignKey(
                        name: "FK_LoanRepayments_EmployeeLoans_EmployeeLoanId",
                        column: x => x.EmployeeLoanId,
                        principalSchema: "pay",
                        principalTable: "EmployeeLoans",
                        principalColumn: "EmployeeLoanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FnfLines",
                schema: "pay",
                columns: table => new
                {
                    FnfLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullAndFinalSettlementId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsDeduction = table.Column<bool>(type: "boolean", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_FnfLines", x => x.FnfLineId);
                    table.ForeignKey(
                        name: "FK_FnfLines_FullAndFinalSettlements_FullAndFinalSettlementId",
                        column: x => x.FullAndFinalSettlementId,
                        principalSchema: "pay",
                        principalTable: "FullAndFinalSettlements",
                        principalColumn: "FullAndFinalSettlementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payslips",
                schema: "pay",
                columns: table => new
                {
                    PayslipId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollRunId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PaidDays = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossDeductions = table.Column<decimal>(type: "numeric", nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.PayslipId);
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "pay",
                        principalTable: "PayrollRuns",
                        principalColumn: "PayrollRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OneTimePayments",
                schema: "pay",
                columns: table => new
                {
                    OneTimePaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    SalaryComponentId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Processed = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_OneTimePayments", x => x.OneTimePaymentId);
                    table.ForeignKey(
                        name: "FK_OneTimePayments_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalSchema: "pay",
                        principalTable: "SalaryComponents",
                        principalColumn: "SalaryComponentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSalaries",
                schema: "pay",
                columns: table => new
                {
                    EmployeeSalaryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    SalaryStructureId = table.Column<long>(type: "bigint", nullable: false),
                    AnnualCtc = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_EmployeeSalaries", x => x.EmployeeSalaryId);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaries_SalaryStructures_SalaryStructureId",
                        column: x => x.SalaryStructureId,
                        principalSchema: "pay",
                        principalTable: "SalaryStructures",
                        principalColumn: "SalaryStructureId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryStructureComponents",
                schema: "pay",
                columns: table => new
                {
                    SalaryStructureComponentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalaryStructureId = table.Column<long>(type: "bigint", nullable: false),
                    SalaryComponentId = table.Column<long>(type: "bigint", nullable: false),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    FlatAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryStructureComponents", x => x.SalaryStructureComponentId);
                    table.ForeignKey(
                        name: "FK_SalaryStructureComponents_SalaryComponents_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalSchema: "pay",
                        principalTable: "SalaryComponents",
                        principalColumn: "SalaryComponentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryStructureComponents_SalaryStructures_SalaryStructureId",
                        column: x => x.SalaryStructureId,
                        principalSchema: "pay",
                        principalTable: "SalaryStructures",
                        principalColumn: "SalaryStructureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentDetails",
                schema: "pay",
                columns: table => new
                {
                    RentDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxDeclarationId = table.Column<long>(type: "bigint", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    RentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    LandlordPan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LandlordName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LandlordAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_RentDetails", x => x.RentDetailId);
                    table.ForeignKey(
                        name: "FK_RentDetails_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalSchema: "pay",
                        principalTable: "TaxDeclarations",
                        principalColumn: "TaxDeclarationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxDeclarationLines",
                schema: "pay",
                columns: table => new
                {
                    TaxDeclarationLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxDeclarationId = table.Column<long>(type: "bigint", nullable: false),
                    Section = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeclaredAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ProofAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    VerifiedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_TaxDeclarationLines", x => x.TaxDeclarationLineId);
                    table.ForeignKey(
                        name: "FK_TaxDeclarationLines_TaxDeclarations_TaxDeclarationId",
                        column: x => x.TaxDeclarationId,
                        principalSchema: "pay",
                        principalTable: "TaxDeclarations",
                        principalColumn: "TaxDeclarationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayslipLines",
                schema: "pay",
                columns: table => new
                {
                    PayslipLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayslipId = table.Column<long>(type: "bigint", nullable: false),
                    SalaryComponentId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayslipLines", x => x.PayslipLineId);
                    table.ForeignKey(
                        name: "FK_PayslipLines_Payslips_PayslipId",
                        column: x => x.PayslipId,
                        principalSchema: "pay",
                        principalTable: "Payslips",
                        principalColumn: "PayslipId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryRevisions",
                schema: "pay",
                columns: table => new
                {
                    SalaryRevisionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeSalaryId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousCtc = table.Column<decimal>(type: "numeric", nullable: false),
                    NewCtc = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ArrearsProcessed = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_SalaryRevisions", x => x.SalaryRevisionId);
                    table.ForeignKey(
                        name: "FK_SalaryRevisions_EmployeeSalaries_EmployeeSalaryId",
                        column: x => x.EmployeeSalaryId,
                        principalSchema: "pay",
                        principalTable: "EmployeeSalaries",
                        principalColumn: "EmployeeSalaryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BonusSettings_CustomerId_OrgId",
                schema: "pay",
                table: "BonusSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_CustomerId_OrgId",
                schema: "pay",
                table: "EditLog",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_ChangedAt",
                schema: "pay",
                table: "EditLog",
                columns: new[] { "OrgId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_EntityName_ChangedAt",
                schema: "pay",
                table: "EditLog",
                columns: new[] { "OrgId", "EntityName", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_RootEntityName_RootEntityKey_ChangedAt",
                schema: "pay",
                table: "EditLog",
                columns: new[] { "OrgId", "RootEntityName", "RootEntityKey", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_UserId_ChangedAt",
                schema: "pay",
                table: "EditLog",
                columns: new[] { "OrgId", "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_CustomerId_OrgId",
                schema: "pay",
                table: "EmployeeLoans",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaries_CustomerId_OrgId",
                schema: "pay",
                table: "EmployeeSalaries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaries_SalaryStructureId",
                schema: "pay",
                table: "EmployeeSalaries",
                column: "SalaryStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "pay",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "pay",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "pay",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EsiSettings_CustomerId_OrgId",
                schema: "pay",
                table: "EsiSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FnfLines_CustomerId_OrgId",
                schema: "pay",
                table: "FnfLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FnfLines_FullAndFinalSettlementId",
                schema: "pay",
                table: "FnfLines",
                column: "FullAndFinalSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_FullAndFinalSettlements_CustomerId_OrgId",
                schema: "pay",
                table: "FullAndFinalSettlements",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GratuitySettings_CustomerId_OrgId",
                schema: "pay",
                table: "GratuitySettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepayments_CustomerId_OrgId",
                schema: "pay",
                table: "LoanRepayments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepayments_EmployeeLoanId",
                schema: "pay",
                table: "LoanRepayments",
                column: "EmployeeLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LwfSettings_CustomerId_OrgId",
                schema: "pay",
                table: "LwfSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyAttendanceInputs_CustomerId_OrgId",
                schema: "pay",
                table: "MonthlyAttendanceInputs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_OneTimePayments_CustomerId_OrgId",
                schema: "pay",
                table: "OneTimePayments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_OneTimePayments_SalaryComponentId",
                schema: "pay",
                table: "OneTimePayments",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayGroups_CustomerId_OrgId",
                schema: "pay",
                table: "PayGroups",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_CustomerId_OrgId",
                schema: "pay",
                table: "PayrollRuns",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayslipLines_PayslipId",
                schema: "pay",
                table: "PayslipLines",
                column: "PayslipId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollRunId",
                schema: "pay",
                table: "Payslips",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PfSettings_CustomerId_OrgId",
                schema: "pay",
                table: "PfSettings",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreviousEmployerIncomes_CustomerId_OrgId",
                schema: "pay",
                table: "PreviousEmployerIncomes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalTaxSlabs_CustomerId_OrgId",
                schema: "pay",
                table: "ProfessionalTaxSlabs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RentDetails_CustomerId_OrgId",
                schema: "pay",
                table: "RentDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RentDetails_TaxDeclarationId",
                schema: "pay",
                table: "RentDetails",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryComponents_CustomerId_OrgId",
                schema: "pay",
                table: "SalaryComponents",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHolds_CustomerId_OrgId",
                schema: "pay",
                table: "SalaryHolds",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRevisions_CustomerId_OrgId",
                schema: "pay",
                table: "SalaryRevisions",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRevisions_EmployeeSalaryId",
                schema: "pay",
                table: "SalaryRevisions",
                column: "EmployeeSalaryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructureComponents_SalaryComponentId",
                schema: "pay",
                table: "SalaryStructureComponents",
                column: "SalaryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructureComponents_SalaryStructureId",
                schema: "pay",
                table: "SalaryStructureComponents",
                column: "SalaryStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryStructures_CustomerId_OrgId",
                schema: "pay",
                table: "SalaryStructures",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryReturns_CustomerId_OrgId",
                schema: "pay",
                table: "StatutoryReturns",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationLines_CustomerId_OrgId",
                schema: "pay",
                table: "TaxDeclarationLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarationLines_TaxDeclarationId",
                schema: "pay",
                table: "TaxDeclarationLines",
                column: "TaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxDeclarations_CustomerId_OrgId",
                schema: "pay",
                table: "TaxDeclarations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRules_CustomerId_OrgId",
                schema: "pay",
                table: "TaxRules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxSlabs_CustomerId_OrgId",
                schema: "pay",
                table: "TaxSlabs",
                columns: new[] { "CustomerId", "OrgId" });
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

            Shared.Kernel.Audit.EditLogMigration.Up(migrationBuilder, "pay");

            // Three tables carry no tenant columns of their own and are guarded
            // through their parent instead: a structure's components through the
            // structure, a payslip through its payroll run, and a payslip line
            // through its payslip's run.
            migrationBuilder.Sql("""
                ALTER TABLE pay."SalaryStructureComponents" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pay."SalaryStructureComponents" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS salarystructurecomponents_tenant_isolation ON pay."SalaryStructureComponents";
                CREATE POLICY salarystructurecomponents_tenant_isolation
                    ON pay."SalaryStructureComponents"
                    FOR ALL
                    USING (EXISTS (
                        SELECT 1 FROM pay."SalaryStructures" s
                        WHERE s."SalaryStructureId" = pay."SalaryStructureComponents"."SalaryStructureId"
                          AND s."CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                          AND s."OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid));

                ALTER TABLE pay."Payslips" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pay."Payslips" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS payslips_tenant_isolation ON pay."Payslips";
                CREATE POLICY payslips_tenant_isolation
                    ON pay."Payslips"
                    FOR ALL
                    USING (EXISTS (
                        SELECT 1 FROM pay."PayrollRuns" r
                        WHERE r."PayrollRunId" = pay."Payslips"."PayrollRunId"
                          AND r."CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                          AND r."OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid));

                ALTER TABLE pay."PayslipLines" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pay."PayslipLines" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS paysliplines_tenant_isolation ON pay."PayslipLines";
                CREATE POLICY paysliplines_tenant_isolation
                    ON pay."PayslipLines"
                    FOR ALL
                    USING (EXISTS (
                        SELECT 1 FROM pay."Payslips" p
                        JOIN pay."PayrollRuns" r ON r."PayrollRunId" = p."PayrollRunId"
                        WHERE p."PayslipId" = pay."PayslipLines"."PayslipId"
                          AND r."CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                          AND r."OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Shared.Kernel.Audit.EditLogMigration.Down(migrationBuilder, "pay");

            migrationBuilder.DropTable(
                name: "BonusSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "EditLog",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "EsiSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "FnfLines",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "GratuitySettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "LoanRepayments",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "LwfSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "MonthlyAttendanceInputs",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "OneTimePayments",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "PayGroups",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "PayslipLines",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "PfSettings",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "PreviousEmployerIncomes",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "ProfessionalTaxSlabs",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "RentDetails",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "SalaryHolds",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "SalaryRevisions",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "SalaryStructureComponents",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "StatutoryReturns",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxDeclarationLines",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxRules",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxSlabs",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "FullAndFinalSettlements",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "EmployeeLoans",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "Payslips",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "EmployeeSalaries",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "SalaryComponents",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "TaxDeclarations",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "PayrollRuns",
                schema: "pay");

            migrationBuilder.DropTable(
                name: "SalaryStructures",
                schema: "pay");
        }

        /// <summary>
        /// Every table guarded by the standard tenant policy, written out rather
        /// than derived at run time: a migration must do the same thing forever.
        /// Taken from the policies the old chains left in a migrated database.
        /// </summary>
        private static readonly (string Schema, string[] Tables)[] RlsTables =
        [
            ("pay",
            [
                "BonusSettings",
                "EmployeeLoans",
                "EmployeeSalaries",
                "ErrorLogs",
                "EsiSettings",
                "FnfLines",
                "FullAndFinalSettlements",
                "GratuitySettings",
                "LoanRepayments",
                "LwfSettings",
                "MonthlyAttendanceInputs",
                "OneTimePayments",
                "PayGroups",
                "PayrollRuns",
                "PfSettings",
                "PreviousEmployerIncomes",
                "ProfessionalTaxSlabs",
                "RentDetails",
                "SalaryComponents",
                "SalaryHolds",
                "SalaryRevisions",
                "SalaryStructures",
                "StatutoryReturns",
                "TaxDeclarationLines",
                "TaxDeclarations",
                "TaxRules",
                "TaxSlabs",
            ]),
        ];
    }
}
