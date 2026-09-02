using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class InitialAdminSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mst");

            migrationBuilder.CreateTable(
                name: "AccountTypes",
                schema: "mst",
                columns: table => new
                {
                    AccountTypeId = table.Column<int>(type: "integer", nullable: false),
                    SystemName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalBalance = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    ReportSection = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.AccountTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Configurations",
                schema: "mst",
                columns: table => new
                {
                    ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DataType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.ConfigId);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                schema: "mst",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CountryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PhoneCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.CountryId);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "mst",
                columns: table => new
                {
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Format = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    SymbolPosition = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.CurrencyId);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "mst",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CountryPrefix = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BillingEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PlanTier = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "HsnSacCodes",
                schema: "mst",
                columns: table => new
                {
                    HsnSacCodeId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CodeType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ChapterCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DefaultGstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DigitLength = table.Column<byte>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsnSacCodes", x => x.HsnSacCodeId);
                });

            migrationBuilder.CreateTable(
                name: "LedgerSources",
                schema: "mst",
                columns: table => new
                {
                    LedgerSourceId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerSources", x => x.LedgerSourceId);
                });

            migrationBuilder.CreateTable(
                name: "LedgerTypes",
                schema: "mst",
                columns: table => new
                {
                    LedgerTypeId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTypes", x => x.LedgerTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Licenses",
                schema: "mst",
                columns: table => new
                {
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxOrganizations = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GraceDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.LicenseId);
                });

            migrationBuilder.CreateTable(
                name: "LoginHistories",
                schema: "mst",
                columns: table => new
                {
                    LoginHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginHistories", x => x.LoginHistoryId);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                schema: "mst",
                columns: table => new
                {
                    MenuId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.MenuId);
                });

            migrationBuilder.CreateTable(
                name: "OrgCurrencies",
                schema: "mst",
                columns: table => new
                {
                    OrgCurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    IsBaseCurrency = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgCurrencies", x => x.OrgCurrencyId);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "mst",
                columns: table => new
                {
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vertical = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrgCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FinancialYearStartMonth = table.Column<int>(type: "integer", nullable: false),
                    Gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Pan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Tan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Tin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Cin = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: true),
                    UdyamNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateId = table.Column<int>(type: "integer", nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CountryId = table.Column<int>(type: "integer", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AllowFreeTextLines = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DiscountLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DiscountBeforeTax = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.OrgId);
                });

            migrationBuilder.CreateTable(
                name: "OtpVerifications",
                schema: "mst",
                columns: table => new
                {
                    OtpVerificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpVerifications", x => x.OtpVerificationId);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                schema: "mst",
                columns: table => new
                {
                    PasswordResetTokenId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.PasswordResetTokenId);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "mst",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "mst",
                columns: table => new
                {
                    RefreshTokenId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.RefreshTokenId);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "mst",
                columns: table => new
                {
                    RolePermissionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.RolePermissionId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "mst",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "SmtpSettings",
                schema: "mst",
                columns: table => new
                {
                    SmtpSettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    FromEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordEncrypted = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmtpSettings", x => x.SmtpSettingsId);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTypes",
                schema: "mst",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsLedgerPosting = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "UserOrganizationRoles",
                schema: "mst",
                columns: table => new
                {
                    UserOrganizationRoleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOrganizationRoles", x => x.UserOrganizationRoleId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "mst",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    MobileConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ThemePreference = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockedOutUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "States",
                schema: "mst",
                columns: table => new
                {
                    StateId = table.Column<int>(type: "integer", nullable: false),
                    CountryId = table.Column<int>(type: "integer", nullable: false),
                    StateCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    StateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_States", x => x.StateId);
                    table.ForeignKey(
                        name: "FK_States_Countries_CountryId",
                        column: x => x.CountryId,
                        principalSchema: "mst",
                        principalTable: "Countries",
                        principalColumn: "CountryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubMenus",
                schema: "mst",
                columns: table => new
                {
                    SubMenuId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RoutePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubMenus", x => x.SubMenuId);
                    table.ForeignKey(
                        name: "FK_SubMenus_Menus_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "mst",
                        principalTable: "Menus",
                        principalColumn: "MenuId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubMenuPermissions",
                schema: "mst",
                columns: table => new
                {
                    SubMenuPermissionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubMenuId = table.Column<int>(type: "integer", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubMenuPermissions", x => x.SubMenuPermissionId);
                    table.ForeignKey(
                        name: "FK_SubMenuPermissions_SubMenus_SubMenuId",
                        column: x => x.SubMenuId,
                        principalSchema: "mst",
                        principalTable: "SubMenus",
                        principalColumn: "SubMenuId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "AccountTypes",
                columns: new[] { "AccountTypeId", "CreatedAt", "CreatedBy", "DisplayName", "IsActive", "ModifiedAt", "ModifiedBy", "NormalBalance", "ReportSection", "SortOrder", "SystemName" },
                values: new object[,]
                {
                    { 1, null, null, "Asset", true, null, null, "Debit", "BalanceSheet", (short)1, "Asset" },
                    { 2, null, null, "Liability", true, null, null, "Credit", "BalanceSheet", (short)2, "Liability" },
                    { 3, null, null, "Equity", true, null, null, "Credit", "BalanceSheet", (short)3, "Equity" },
                    { 4, null, null, "Income", true, null, null, "Credit", "ProfitAndLoss", (short)4, "Income" },
                    { 5, null, null, "Expense", true, null, null, "Debit", "ProfitAndLoss", (short)5, "Expense" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), "Formatting", "unitPrice.decimals", null, null, "Number", "Decimal places for unit price inputs", true, null, null, "Unit Price Decimals", null, "2" },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), "Formatting", "quantity.decimals", null, null, "Number", "Decimal places for quantity inputs", true, null, null, "Quantity Decimals", null, "2" },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), "Documents", "sales.dueDays", null, null, "Number", "Default payment terms on invoices", true, null, null, "Sales Due Days", null, "30" },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), "Documents", "purchase.dueDays", null, null, "Number", "Default payment terms on bills", true, null, null, "Purchase Due Days", null, "30" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Countries",
                columns: new[] { "CountryId", "CountryCode", "CountryName", "CreatedAt", "CreatedBy", "CurrencyCode", "IsActive", "ModifiedAt", "ModifiedBy", "PhoneCode" },
                values: new object[,]
                {
                    { 1, "IN", "India", null, null, "INR", true, null, null, "+91" },
                    { 2, "US", "United States", null, null, "USD", true, null, null, "+1" },
                    { 3, "GB", "United Kingdom", null, null, "GBP", true, null, null, "+44" },
                    { 4, "AE", "United Arab Emirates", null, null, "AED", true, null, null, "+971" },
                    { 5, "SG", "Singapore", null, null, "SGD", true, null, null, "+65" },
                    { 6, "AF", "Afghanistan", null, null, "AFN", true, null, null, "+93" },
                    { 7, "AX", "Aland Islands", null, null, "EUR", true, null, null, "+358" },
                    { 8, "AL", "Albania", null, null, "ALL", true, null, null, "+355" },
                    { 9, "DZ", "Algeria", null, null, "DZD", true, null, null, "+213" },
                    { 10, "AS", "American Samoa", null, null, "USD", true, null, null, "+1" },
                    { 11, "AD", "Andorra", null, null, "EUR", true, null, null, "+376" },
                    { 12, "AO", "Angola", null, null, "AOA", true, null, null, "+244" },
                    { 13, "AI", "Anguilla", null, null, "XCD", true, null, null, "+1" },
                    { 14, "AQ", "Antarctica", null, null, "AAD", true, null, null, "+672" },
                    { 15, "AG", "Antigua and Barbuda", null, null, "XCD", true, null, null, "+1" },
                    { 16, "AR", "Argentina", null, null, "ARS", true, null, null, "+54" },
                    { 17, "AM", "Armenia", null, null, "AMD", true, null, null, "+374" },
                    { 18, "AW", "Aruba", null, null, "AWG", true, null, null, "+297" },
                    { 19, "AU", "Australia", null, null, "AUD", true, null, null, "+61" },
                    { 20, "AT", "Austria", null, null, "EUR", true, null, null, "+43" },
                    { 21, "AZ", "Azerbaijan", null, null, "AZN", true, null, null, "+994" },
                    { 22, "BH", "Bahrain", null, null, "BHD", true, null, null, "+973" },
                    { 23, "BD", "Bangladesh", null, null, "BDT", true, null, null, "+880" },
                    { 24, "BB", "Barbados", null, null, "BBD", true, null, null, "+1" },
                    { 25, "BY", "Belarus", null, null, "BYN", true, null, null, "+375" },
                    { 26, "BE", "Belgium", null, null, "EUR", true, null, null, "+32" },
                    { 27, "BZ", "Belize", null, null, "BZD", true, null, null, "+501" },
                    { 28, "BJ", "Benin", null, null, "XOF", true, null, null, "+229" },
                    { 29, "BM", "Bermuda", null, null, "BMD", true, null, null, "+1" },
                    { 30, "BT", "Bhutan", null, null, "BTN", true, null, null, "+975" },
                    { 31, "BO", "Bolivia", null, null, "BOB", true, null, null, "+591" },
                    { 32, "BQ", "Bonaire, Sint Eustatius and Saba", null, null, "USD", true, null, null, "+599" },
                    { 33, "BA", "Bosnia and Herzegovina", null, null, "BAM", true, null, null, "+387" },
                    { 34, "BW", "Botswana", null, null, "BWP", true, null, null, "+267" },
                    { 35, "BV", "Bouvet Island", null, null, "NOK", true, null, null, "+0055" },
                    { 36, "BR", "Brazil", null, null, "BRL", true, null, null, "+55" },
                    { 37, "IO", "British Indian Ocean Territory", null, null, "USD", true, null, null, "+246" },
                    { 38, "BN", "Brunei", null, null, "BND", true, null, null, "+673" },
                    { 39, "BG", "Bulgaria", null, null, "EUR", true, null, null, "+359" },
                    { 40, "BF", "Burkina Faso", null, null, "XOF", true, null, null, "+226" },
                    { 41, "BI", "Burundi", null, null, "BIF", true, null, null, "+257" },
                    { 42, "KH", "Cambodia", null, null, "KHR", true, null, null, "+855" },
                    { 43, "CM", "Cameroon", null, null, "XAF", true, null, null, "+237" },
                    { 44, "CA", "Canada", null, null, "CAD", true, null, null, "+1" },
                    { 45, "CV", "Cape Verde", null, null, "CVE", true, null, null, "+238" },
                    { 46, "KY", "Cayman Islands", null, null, "KYD", true, null, null, "+1" },
                    { 47, "CF", "Central African Republic", null, null, "XAF", true, null, null, "+236" },
                    { 48, "TD", "Chad", null, null, "XAF", true, null, null, "+235" },
                    { 49, "CL", "Chile", null, null, "CLP", true, null, null, "+56" },
                    { 50, "CN", "China", null, null, "CNY", true, null, null, "+86" },
                    { 51, "CX", "Christmas Island", null, null, "AUD", true, null, null, "+61" },
                    { 52, "CC", "Cocos (Keeling) Islands", null, null, "AUD", true, null, null, "+61" },
                    { 53, "CO", "Colombia", null, null, "COP", true, null, null, "+57" },
                    { 54, "KM", "Comoros", null, null, "KMF", true, null, null, "+269" },
                    { 55, "CG", "Congo", null, null, "CDF", true, null, null, "+242" },
                    { 56, "CK", "Cook Islands", null, null, "NZD", true, null, null, "+682" },
                    { 57, "CR", "Costa Rica", null, null, "CRC", true, null, null, "+506" },
                    { 58, "HR", "Croatia", null, null, "EUR", true, null, null, "+385" },
                    { 59, "CU", "Cuba", null, null, "CUP", true, null, null, "+53" },
                    { 60, "CW", "Curaçao", null, null, "ANG", true, null, null, "+599" },
                    { 61, "CY", "Cyprus", null, null, "EUR", true, null, null, "+357" },
                    { 62, "CZ", "Czech Republic", null, null, "CZK", true, null, null, "+420" },
                    { 63, "CD", "Democratic Republic of the Congo", null, null, "CDF", true, null, null, "+243" },
                    { 64, "DK", "Denmark", null, null, "DKK", true, null, null, "+45" },
                    { 65, "DJ", "Djibouti", null, null, "DJF", true, null, null, "+253" },
                    { 66, "DM", "Dominica", null, null, "XCD", true, null, null, "+1" },
                    { 67, "DO", "Dominican Republic", null, null, "DOP", true, null, null, "+1" },
                    { 68, "EC", "Ecuador", null, null, "USD", true, null, null, "+593" },
                    { 69, "EG", "Egypt", null, null, "EGP", true, null, null, "+20" },
                    { 70, "SV", "El Salvador", null, null, "USD", true, null, null, "+503" },
                    { 71, "GQ", "Equatorial Guinea", null, null, "XAF", true, null, null, "+240" },
                    { 72, "ER", "Eritrea", null, null, "ERN", true, null, null, "+291" },
                    { 73, "EE", "Estonia", null, null, "EUR", true, null, null, "+372" },
                    { 74, "SZ", "Eswatini", null, null, "SZL", true, null, null, "+268" },
                    { 75, "ET", "Ethiopia", null, null, "ETB", true, null, null, "+251" },
                    { 76, "FK", "Falkland Islands", null, null, "FKP", true, null, null, "+500" },
                    { 77, "FO", "Faroe Islands", null, null, "DKK", true, null, null, "+298" },
                    { 78, "FJ", "Fiji Islands", null, null, "FJD", true, null, null, "+679" },
                    { 79, "FI", "Finland", null, null, "EUR", true, null, null, "+358" },
                    { 80, "FR", "France", null, null, "EUR", true, null, null, "+33" },
                    { 81, "GF", "French Guiana", null, null, "EUR", true, null, null, "+594" },
                    { 82, "PF", "French Polynesia", null, null, "XPF", true, null, null, "+689" },
                    { 83, "TF", "French Southern Territories", null, null, "EUR", true, null, null, "+262" },
                    { 84, "GA", "Gabon", null, null, "XAF", true, null, null, "+241" },
                    { 85, "GE", "Georgia", null, null, "GEL", true, null, null, "+995" },
                    { 86, "DE", "Germany", null, null, "EUR", true, null, null, "+49" },
                    { 87, "GH", "Ghana", null, null, "GHS", true, null, null, "+233" },
                    { 88, "GI", "Gibraltar", null, null, "GIP", true, null, null, "+350" },
                    { 89, "GR", "Greece", null, null, "EUR", true, null, null, "+30" },
                    { 90, "GL", "Greenland", null, null, "DKK", true, null, null, "+299" },
                    { 91, "GD", "Grenada", null, null, "XCD", true, null, null, "+1" },
                    { 92, "GP", "Guadeloupe", null, null, "EUR", true, null, null, "+590" },
                    { 93, "GU", "Guam", null, null, "USD", true, null, null, "+1" },
                    { 94, "GT", "Guatemala", null, null, "GTQ", true, null, null, "+502" },
                    { 95, "GG", "Guernsey", null, null, "GBP", true, null, null, "+44" },
                    { 96, "GN", "Guinea", null, null, "GNF", true, null, null, "+224" },
                    { 97, "GW", "Guinea-Bissau", null, null, "XOF", true, null, null, "+245" },
                    { 98, "GY", "Guyana", null, null, "GYD", true, null, null, "+592" },
                    { 99, "HT", "Haiti", null, null, "HTG", true, null, null, "+509" },
                    { 100, "HM", "Heard Island and McDonald Islands", null, null, "AUD", true, null, null, "+672" },
                    { 101, "HN", "Honduras", null, null, "HNL", true, null, null, "+504" },
                    { 102, "HK", "Hong Kong S.A.R.", null, null, "HKD", true, null, null, "+852" },
                    { 103, "HU", "Hungary", null, null, "HUF", true, null, null, "+36" },
                    { 104, "IS", "Iceland", null, null, "ISK", true, null, null, "+354" },
                    { 105, "ID", "Indonesia", null, null, "IDR", true, null, null, "+62" },
                    { 106, "IR", "Iran", null, null, "IRR", true, null, null, "+98" },
                    { 107, "IQ", "Iraq", null, null, "IQD", true, null, null, "+964" },
                    { 108, "IE", "Ireland", null, null, "EUR", true, null, null, "+353" },
                    { 109, "IL", "Israel", null, null, "ILS", true, null, null, "+972" },
                    { 110, "IT", "Italy", null, null, "EUR", true, null, null, "+39" },
                    { 111, "CI", "Ivory Coast", null, null, "XOF", true, null, null, "+225" },
                    { 112, "JM", "Jamaica", null, null, "JMD", true, null, null, "+1" },
                    { 113, "JP", "Japan", null, null, "JPY", true, null, null, "+81" },
                    { 114, "JE", "Jersey", null, null, "GBP", true, null, null, "+44" },
                    { 115, "JO", "Jordan", null, null, "JOD", true, null, null, "+962" },
                    { 116, "KZ", "Kazakhstan", null, null, "KZT", true, null, null, "+7" },
                    { 117, "KE", "Kenya", null, null, "KES", true, null, null, "+254" },
                    { 118, "KI", "Kiribati", null, null, "AUD", true, null, null, "+686" },
                    { 119, "XK", "Kosovo", null, null, "EUR", true, null, null, "+383" },
                    { 120, "KW", "Kuwait", null, null, "KWD", true, null, null, "+965" },
                    { 121, "KG", "Kyrgyzstan", null, null, "KGS", true, null, null, "+996" },
                    { 122, "LA", "Laos", null, null, "LAK", true, null, null, "+856" },
                    { 123, "LV", "Latvia", null, null, "EUR", true, null, null, "+371" },
                    { 124, "LB", "Lebanon", null, null, "LBP", true, null, null, "+961" },
                    { 125, "LS", "Lesotho", null, null, "LSL", true, null, null, "+266" },
                    { 126, "LR", "Liberia", null, null, "LRD", true, null, null, "+231" },
                    { 127, "LY", "Libya", null, null, "LYD", true, null, null, "+218" },
                    { 128, "LI", "Liechtenstein", null, null, "CHF", true, null, null, "+423" },
                    { 129, "LT", "Lithuania", null, null, "EUR", true, null, null, "+370" },
                    { 130, "LU", "Luxembourg", null, null, "EUR", true, null, null, "+352" },
                    { 131, "MO", "Macau S.A.R.", null, null, "MOP", true, null, null, "+853" },
                    { 132, "MG", "Madagascar", null, null, "MGA", true, null, null, "+261" },
                    { 133, "MW", "Malawi", null, null, "MWK", true, null, null, "+265" },
                    { 134, "MY", "Malaysia", null, null, "MYR", true, null, null, "+60" },
                    { 135, "MV", "Maldives", null, null, "MVR", true, null, null, "+960" },
                    { 136, "ML", "Mali", null, null, "XOF", true, null, null, "+223" },
                    { 137, "MT", "Malta", null, null, "EUR", true, null, null, "+356" },
                    { 138, "IM", "Man (Isle of)", null, null, "GBP", true, null, null, "+44" },
                    { 139, "MH", "Marshall Islands", null, null, "USD", true, null, null, "+692" },
                    { 140, "MQ", "Martinique", null, null, "EUR", true, null, null, "+596" },
                    { 141, "MR", "Mauritania", null, null, "MRU", true, null, null, "+222" },
                    { 142, "MU", "Mauritius", null, null, "MUR", true, null, null, "+230" },
                    { 143, "YT", "Mayotte", null, null, "EUR", true, null, null, "+262" },
                    { 144, "MX", "Mexico", null, null, "MXN", true, null, null, "+52" },
                    { 145, "FM", "Micronesia", null, null, "USD", true, null, null, "+691" },
                    { 146, "MD", "Moldova", null, null, "MDL", true, null, null, "+373" },
                    { 147, "MC", "Monaco", null, null, "EUR", true, null, null, "+377" },
                    { 148, "MN", "Mongolia", null, null, "MNT", true, null, null, "+976" },
                    { 149, "ME", "Montenegro", null, null, "EUR", true, null, null, "+382" },
                    { 150, "MS", "Montserrat", null, null, "XCD", true, null, null, "+1" },
                    { 151, "MA", "Morocco", null, null, "MAD", true, null, null, "+212" },
                    { 152, "MZ", "Mozambique", null, null, "MZN", true, null, null, "+258" },
                    { 153, "MM", "Myanmar", null, null, "MMK", true, null, null, "+95" },
                    { 154, "NA", "Namibia", null, null, "NAD", true, null, null, "+264" },
                    { 155, "NR", "Nauru", null, null, "AUD", true, null, null, "+674" },
                    { 156, "NP", "Nepal", null, null, "NPR", true, null, null, "+977" },
                    { 157, "NL", "Netherlands", null, null, "EUR", true, null, null, "+31" },
                    { 158, "NC", "New Caledonia", null, null, "XPF", true, null, null, "+687" },
                    { 159, "NZ", "New Zealand", null, null, "NZD", true, null, null, "+64" },
                    { 160, "NI", "Nicaragua", null, null, "NIO", true, null, null, "+505" },
                    { 161, "NE", "Niger", null, null, "XOF", true, null, null, "+227" },
                    { 162, "NG", "Nigeria", null, null, "NGN", true, null, null, "+234" },
                    { 163, "NU", "Niue", null, null, "NZD", true, null, null, "+683" },
                    { 164, "NF", "Norfolk Island", null, null, "AUD", true, null, null, "+672" },
                    { 165, "KP", "North Korea", null, null, "KPW", true, null, null, "+850" },
                    { 166, "MK", "North Macedonia", null, null, "MKD", true, null, null, "+389" },
                    { 167, "MP", "Northern Mariana Islands", null, null, "USD", true, null, null, "+1" },
                    { 168, "NO", "Norway", null, null, "NOK", true, null, null, "+47" },
                    { 169, "OM", "Oman", null, null, "OMR", true, null, null, "+968" },
                    { 170, "PK", "Pakistan", null, null, "PKR", true, null, null, "+92" },
                    { 171, "PW", "Palau", null, null, "USD", true, null, null, "+680" },
                    { 172, "PS", "Palestinian Territory Occupied", null, null, "ILS", true, null, null, "+970" },
                    { 173, "PA", "Panama", null, null, "PAB", true, null, null, "+507" },
                    { 174, "PG", "Papua New Guinea", null, null, "PGK", true, null, null, "+675" },
                    { 175, "PY", "Paraguay", null, null, "PYG", true, null, null, "+595" },
                    { 176, "PE", "Peru", null, null, "PEN", true, null, null, "+51" },
                    { 177, "PH", "Philippines", null, null, "PHP", true, null, null, "+63" },
                    { 178, "PN", "Pitcairn Island", null, null, "NZD", true, null, null, "+870" },
                    { 179, "PL", "Poland", null, null, "PLN", true, null, null, "+48" },
                    { 180, "PT", "Portugal", null, null, "EUR", true, null, null, "+351" },
                    { 181, "PR", "Puerto Rico", null, null, "USD", true, null, null, "+1" },
                    { 182, "QA", "Qatar", null, null, "QAR", true, null, null, "+974" },
                    { 183, "RE", "Reunion", null, null, "EUR", true, null, null, "+262" },
                    { 184, "RO", "Romania", null, null, "RON", true, null, null, "+40" },
                    { 185, "RU", "Russia", null, null, "RUB", true, null, null, "+7" },
                    { 186, "RW", "Rwanda", null, null, "RWF", true, null, null, "+250" },
                    { 187, "SH", "Saint Helena", null, null, "SHP", true, null, null, "+290" },
                    { 188, "KN", "Saint Kitts and Nevis", null, null, "XCD", true, null, null, "+1" },
                    { 189, "LC", "Saint Lucia", null, null, "XCD", true, null, null, "+1" },
                    { 190, "PM", "Saint Pierre and Miquelon", null, null, "EUR", true, null, null, "+508" },
                    { 191, "VC", "Saint Vincent and the Grenadines", null, null, "XCD", true, null, null, "+1" },
                    { 192, "BL", "Saint-Barthelemy", null, null, "EUR", true, null, null, "+590" },
                    { 193, "MF", "Saint-Martin (French part)", null, null, "EUR", true, null, null, "+590" },
                    { 194, "WS", "Samoa", null, null, "WST", true, null, null, "+685" },
                    { 195, "SM", "San Marino", null, null, "EUR", true, null, null, "+378" },
                    { 196, "ST", "Sao Tome and Principe", null, null, "STN", true, null, null, "+239" },
                    { 197, "SA", "Saudi Arabia", null, null, "SAR", true, null, null, "+966" },
                    { 198, "SN", "Senegal", null, null, "XOF", true, null, null, "+221" },
                    { 199, "RS", "Serbia", null, null, "RSD", true, null, null, "+381" },
                    { 200, "SC", "Seychelles", null, null, "SCR", true, null, null, "+248" },
                    { 201, "SL", "Sierra Leone", null, null, "SLL", true, null, null, "+232" },
                    { 202, "SX", "Sint Maarten (Dutch part)", null, null, "ANG", true, null, null, "+1721" },
                    { 203, "SK", "Slovakia", null, null, "EUR", true, null, null, "+421" },
                    { 204, "SI", "Slovenia", null, null, "EUR", true, null, null, "+386" },
                    { 205, "SB", "Solomon Islands", null, null, "SBD", true, null, null, "+677" },
                    { 206, "SO", "Somalia", null, null, "SOS", true, null, null, "+252" },
                    { 207, "ZA", "South Africa", null, null, "ZAR", true, null, null, "+27" },
                    { 208, "GS", "South Georgia", null, null, "GBP", true, null, null, "+500" },
                    { 209, "KR", "South Korea", null, null, "KRW", true, null, null, "+82" },
                    { 210, "SS", "South Sudan", null, null, "SSP", true, null, null, "+211" },
                    { 211, "ES", "Spain", null, null, "EUR", true, null, null, "+34" },
                    { 212, "LK", "Sri Lanka", null, null, "LKR", true, null, null, "+94" },
                    { 213, "SD", "Sudan", null, null, "SDG", true, null, null, "+249" },
                    { 214, "SR", "Suriname", null, null, "SRD", true, null, null, "+597" },
                    { 215, "SJ", "Svalbard and Jan Mayen Islands", null, null, "NOK", true, null, null, "+47" },
                    { 216, "SE", "Sweden", null, null, "SEK", true, null, null, "+46" },
                    { 217, "CH", "Switzerland", null, null, "CHF", true, null, null, "+41" },
                    { 218, "SY", "Syria", null, null, "SYP", true, null, null, "+963" },
                    { 219, "TW", "Taiwan", null, null, "TWD", true, null, null, "+886" },
                    { 220, "TJ", "Tajikistan", null, null, "TJS", true, null, null, "+992" },
                    { 221, "TZ", "Tanzania", null, null, "TZS", true, null, null, "+255" },
                    { 222, "TH", "Thailand", null, null, "THB", true, null, null, "+66" },
                    { 223, "BS", "The Bahamas", null, null, "BSD", true, null, null, "+1" },
                    { 224, "GM", "The Gambia", null, null, "GMD", true, null, null, "+220" },
                    { 225, "TL", "Timor-Leste", null, null, "USD", true, null, null, "+670" },
                    { 226, "TG", "Togo", null, null, "XOF", true, null, null, "+228" },
                    { 227, "TK", "Tokelau", null, null, "NZD", true, null, null, "+690" },
                    { 228, "TO", "Tonga", null, null, "TOP", true, null, null, "+676" },
                    { 229, "TT", "Trinidad and Tobago", null, null, "TTD", true, null, null, "+1" },
                    { 230, "TN", "Tunisia", null, null, "TND", true, null, null, "+216" },
                    { 231, "TR", "Turkey", null, null, "TRY", true, null, null, "+90" },
                    { 232, "TM", "Turkmenistan", null, null, "TMT", true, null, null, "+993" },
                    { 233, "TC", "Turks and Caicos Islands", null, null, "USD", true, null, null, "+1" },
                    { 234, "TV", "Tuvalu", null, null, "AUD", true, null, null, "+688" },
                    { 235, "UG", "Uganda", null, null, "UGX", true, null, null, "+256" },
                    { 236, "UA", "Ukraine", null, null, "UAH", true, null, null, "+380" },
                    { 237, "UM", "United States Minor Outlying Islands", null, null, "USD", true, null, null, "+1" },
                    { 238, "UY", "Uruguay", null, null, "UYU", true, null, null, "+598" },
                    { 239, "UZ", "Uzbekistan", null, null, "UZS", true, null, null, "+998" },
                    { 240, "VU", "Vanuatu", null, null, "VUV", true, null, null, "+678" },
                    { 241, "VA", "Vatican City State (Holy See)", null, null, "EUR", true, null, null, "+379" },
                    { 242, "VE", "Venezuela", null, null, "VES", true, null, null, "+58" },
                    { 243, "VN", "Vietnam", null, null, "VND", true, null, null, "+84" },
                    { 244, "VG", "Virgin Islands (British)", null, null, "USD", true, null, null, "+1" },
                    { 245, "VI", "Virgin Islands (US)", null, null, "USD", true, null, null, "+1" },
                    { 246, "WF", "Wallis and Futuna Islands", null, null, "XPF", true, null, null, "+681" },
                    { 247, "EH", "Western Sahara", null, null, "MAD", true, null, null, "+212" },
                    { 248, "YE", "Yemen", null, null, "YER", true, null, null, "+967" },
                    { 249, "ZM", "Zambia", null, null, "ZMW", true, null, null, "+260" },
                    { 250, "ZW", "Zimbabwe", null, null, "ZWL", true, null, null, "+263" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Currencies",
                columns: new[] { "CurrencyId", "Code", "CreatedAt", "CreatedBy", "DecimalPlaces", "Format", "IsActive", "ModifiedAt", "ModifiedBy", "Name", "Symbol", "SymbolPosition" },
                values: new object[,]
                {
                    { 1, "INR", null, null, 2, "##,##,##,##0.00", true, null, null, "Indian rupee", "₹", "Prefix" },
                    { 2, "USD", null, null, 2, "###,###,##0.00", true, null, null, "United States dollar", "$", "Prefix" },
                    { 3, "GBP", null, null, 2, "###,###,##0.00", true, null, null, "British pound", "£", "Prefix" },
                    { 4, "AED", null, null, 2, "###,###,##0.00", true, null, null, "United Arab Emirates dirham", "إ.د", "Prefix" },
                    { 5, "SGD", null, null, 2, "###,###,##0.00", true, null, null, "Singapore dollar", "$", "Prefix" },
                    { 6, "AAD", null, null, 2, "###,###,##0.00", true, null, null, "Antarctican dollar", "$", "Prefix" },
                    { 7, "AFN", null, null, 2, "###,###,##0.00", true, null, null, "Afghan afghani", "؋", "Prefix" },
                    { 8, "ALL", null, null, 2, "###,###,##0.00", true, null, null, "Albanian lek", "Lek", "Prefix" },
                    { 9, "AMD", null, null, 2, "###,###,##0.00", true, null, null, "Armenian dram", "֏", "Prefix" },
                    { 10, "ANG", null, null, 2, "###,###,##0.00", true, null, null, "Netherlands Antillean guilder", "ƒ", "Prefix" },
                    { 11, "AOA", null, null, 2, "###,###,##0.00", true, null, null, "Angolan kwanza", "Kz", "Prefix" },
                    { 12, "ARS", null, null, 2, "###,###,##0.00", true, null, null, "Argentine peso", "$", "Prefix" },
                    { 13, "AUD", null, null, 2, "###,###,##0.00", true, null, null, "Australian dollar", "$", "Prefix" },
                    { 14, "AWG", null, null, 2, "###,###,##0.00", true, null, null, "Aruban florin", "ƒ", "Prefix" },
                    { 15, "AZN", null, null, 2, "###,###,##0.00", true, null, null, "Azerbaijani manat", "m", "Prefix" },
                    { 16, "BAM", null, null, 2, "###,###,##0.00", true, null, null, "Bosnia and Herzegovina convertible mark", "KM", "Prefix" },
                    { 17, "BBD", null, null, 2, "###,###,##0.00", true, null, null, "Barbadian dollar", "Bds$", "Prefix" },
                    { 18, "BDT", null, null, 2, "##,##,##,##0.00", true, null, null, "Bangladeshi taka", "৳", "Prefix" },
                    { 19, "BHD", null, null, 3, "###,###,##0.000", true, null, null, "Bahraini dinar", ".د.ب", "Prefix" },
                    { 20, "BIF", null, null, 0, "###,###,##0", true, null, null, "Burundian franc", "FBu", "Prefix" },
                    { 21, "BMD", null, null, 2, "###,###,##0.00", true, null, null, "Bermudian dollar", "$", "Prefix" },
                    { 22, "BND", null, null, 2, "###,###,##0.00", true, null, null, "Brunei dollar", "B$", "Prefix" },
                    { 23, "BOB", null, null, 2, "###,###,##0.00", true, null, null, "Bolivian boliviano", "Bs.", "Prefix" },
                    { 24, "BRL", null, null, 2, "###,###,##0.00", true, null, null, "Brazilian real", "R$", "Prefix" },
                    { 25, "BSD", null, null, 2, "###,###,##0.00", true, null, null, "Bahamian dollar", "B$", "Prefix" },
                    { 26, "BTN", null, null, 2, "###,###,##0.00", true, null, null, "Bhutanese ngultrum", "Nu.", "Prefix" },
                    { 27, "BWP", null, null, 2, "###,###,##0.00", true, null, null, "Botswana pula", "P", "Prefix" },
                    { 28, "BYN", null, null, 2, "###,###,##0.00", true, null, null, "Belarusian ruble", "Br", "Prefix" },
                    { 29, "BZD", null, null, 2, "###,###,##0.00", true, null, null, "Belize dollar", "$", "Prefix" },
                    { 30, "CAD", null, null, 2, "###,###,##0.00", true, null, null, "Canadian dollar", "$", "Prefix" },
                    { 31, "CDF", null, null, 2, "###,###,##0.00", true, null, null, "Congolese Franc", "FC", "Prefix" },
                    { 32, "CHF", null, null, 2, "###,###,##0.00", true, null, null, "Swiss franc", "CHf", "Prefix" },
                    { 33, "CLP", null, null, 0, "###,###,##0", true, null, null, "Chilean peso", "$", "Prefix" },
                    { 34, "CNY", null, null, 2, "###,###,##0.00", true, null, null, "Chinese yuan", "¥", "Prefix" },
                    { 35, "COP", null, null, 2, "###,###,##0.00", true, null, null, "Colombian peso", "$", "Prefix" },
                    { 36, "CRC", null, null, 2, "###,###,##0.00", true, null, null, "Costa Rican colón", "₡", "Prefix" },
                    { 37, "CUP", null, null, 2, "###,###,##0.00", true, null, null, "Cuban peso", "$", "Prefix" },
                    { 38, "CVE", null, null, 2, "###,###,##0.00", true, null, null, "Cape Verdean escudo", "$", "Prefix" },
                    { 39, "CZK", null, null, 2, "###,###,##0.00", true, null, null, "Czech koruna", "Kč", "Prefix" },
                    { 40, "DJF", null, null, 0, "###,###,##0", true, null, null, "Djiboutian franc", "Fdj", "Prefix" },
                    { 41, "DKK", null, null, 2, "###,###,##0.00", true, null, null, "Danish krone", "Kr.", "Prefix" },
                    { 42, "DOP", null, null, 2, "###,###,##0.00", true, null, null, "Dominican peso", "$", "Prefix" },
                    { 43, "DZD", null, null, 2, "###,###,##0.00", true, null, null, "Algerian dinar", "دج", "Prefix" },
                    { 44, "EGP", null, null, 2, "###,###,##0.00", true, null, null, "Egyptian pound", "ج.م", "Prefix" },
                    { 45, "ERN", null, null, 2, "###,###,##0.00", true, null, null, "Eritrean nakfa", "Nfk", "Prefix" },
                    { 46, "ETB", null, null, 2, "###,###,##0.00", true, null, null, "Ethiopian birr", "Nkf", "Prefix" },
                    { 47, "EUR", null, null, 2, "###,###,##0.00", true, null, null, "Euro", "€", "Prefix" },
                    { 48, "FJD", null, null, 2, "###,###,##0.00", true, null, null, "Fijian dollar", "FJ$", "Prefix" },
                    { 49, "FKP", null, null, 2, "###,###,##0.00", true, null, null, "Falkland Islands pound", "£", "Prefix" },
                    { 50, "GEL", null, null, 2, "###,###,##0.00", true, null, null, "Georgian lari", "ლ", "Prefix" },
                    { 51, "GHS", null, null, 2, "###,###,##0.00", true, null, null, "Ghanaian cedi", "GH₵", "Prefix" },
                    { 52, "GIP", null, null, 2, "###,###,##0.00", true, null, null, "Gibraltar pound", "£", "Prefix" },
                    { 53, "GMD", null, null, 2, "###,###,##0.00", true, null, null, "Gambian dalasi", "D", "Prefix" },
                    { 54, "GNF", null, null, 0, "###,###,##0", true, null, null, "Guinean franc", "FG", "Prefix" },
                    { 55, "GTQ", null, null, 2, "###,###,##0.00", true, null, null, "Guatemalan quetzal", "Q", "Prefix" },
                    { 56, "GYD", null, null, 2, "###,###,##0.00", true, null, null, "Guyanese dollar", "$", "Prefix" },
                    { 57, "HKD", null, null, 2, "###,###,##0.00", true, null, null, "Hong Kong dollar", "$", "Prefix" },
                    { 58, "HNL", null, null, 2, "###,###,##0.00", true, null, null, "Honduran lempira", "L", "Prefix" },
                    { 59, "HTG", null, null, 2, "###,###,##0.00", true, null, null, "Haitian gourde", "G", "Prefix" },
                    { 60, "HUF", null, null, 2, "###,###,##0.00", true, null, null, "Hungarian forint", "Ft", "Prefix" },
                    { 61, "IDR", null, null, 2, "###,###,##0.00", true, null, null, "Indonesian rupiah", "Rp", "Prefix" },
                    { 62, "ILS", null, null, 2, "###,###,##0.00", true, null, null, "Israeli new shekel", "₪", "Prefix" },
                    { 63, "IQD", null, null, 3, "###,###,##0.000", true, null, null, "Iraqi dinar", "د.ع", "Prefix" },
                    { 64, "IRR", null, null, 2, "###,###,##0.00", true, null, null, "Iranian rial", "﷼", "Prefix" },
                    { 65, "ISK", null, null, 0, "###,###,##0", true, null, null, "Icelandic króna", "ko", "Prefix" },
                    { 66, "JMD", null, null, 2, "###,###,##0.00", true, null, null, "Jamaican dollar", "J$", "Prefix" },
                    { 67, "JOD", null, null, 3, "###,###,##0.000", true, null, null, "Jordanian dinar", "ا.د", "Prefix" },
                    { 68, "JPY", null, null, 0, "###,###,##0", true, null, null, "Japanese yen", "¥", "Prefix" },
                    { 69, "KES", null, null, 2, "###,###,##0.00", true, null, null, "Kenyan shilling", "KSh", "Prefix" },
                    { 70, "KGS", null, null, 2, "###,###,##0.00", true, null, null, "Kyrgyzstani som", "лв", "Prefix" },
                    { 71, "KHR", null, null, 2, "###,###,##0.00", true, null, null, "Cambodian riel", "KHR", "Prefix" },
                    { 72, "KMF", null, null, 0, "###,###,##0", true, null, null, "Comorian franc", "CF", "Prefix" },
                    { 73, "KPW", null, null, 2, "###,###,##0.00", true, null, null, "North Korean Won", "₩", "Prefix" },
                    { 74, "KRW", null, null, 0, "###,###,##0", true, null, null, "Won", "₩", "Prefix" },
                    { 75, "KWD", null, null, 3, "###,###,##0.000", true, null, null, "Kuwaiti dinar", "ك.د", "Prefix" },
                    { 76, "KYD", null, null, 2, "###,###,##0.00", true, null, null, "Cayman Islands dollar", "$", "Prefix" },
                    { 77, "KZT", null, null, 2, "###,###,##0.00", true, null, null, "Kazakhstani tenge", "лв", "Prefix" },
                    { 78, "LAK", null, null, 2, "###,###,##0.00", true, null, null, "Lao kip", "₭", "Prefix" },
                    { 79, "LBP", null, null, 2, "###,###,##0.00", true, null, null, "Lebanese pound", "£", "Prefix" },
                    { 80, "LKR", null, null, 2, "##,##,##,##0.00", true, null, null, "Sri Lankan rupee", "Rs", "Prefix" },
                    { 81, "LRD", null, null, 2, "###,###,##0.00", true, null, null, "Liberian dollar", "$", "Prefix" },
                    { 82, "LSL", null, null, 2, "###,###,##0.00", true, null, null, "Lesotho loti", "L", "Prefix" },
                    { 83, "LYD", null, null, 3, "###,###,##0.000", true, null, null, "Libyan dinar", "د.ل", "Prefix" },
                    { 84, "MAD", null, null, 2, "###,###,##0.00", true, null, null, "Moroccan dirham", "DH", "Prefix" },
                    { 85, "MDL", null, null, 2, "###,###,##0.00", true, null, null, "Moldovan leu", "L", "Prefix" },
                    { 86, "MGA", null, null, 2, "###,###,##0.00", true, null, null, "Malagasy ariary", "Ar", "Prefix" },
                    { 87, "MKD", null, null, 2, "###,###,##0.00", true, null, null, "Denar", "ден", "Prefix" },
                    { 88, "MMK", null, null, 2, "###,###,##0.00", true, null, null, "Burmese kyat", "K", "Prefix" },
                    { 89, "MNT", null, null, 2, "###,###,##0.00", true, null, null, "Mongolian tögrög", "₮", "Prefix" },
                    { 90, "MOP", null, null, 2, "###,###,##0.00", true, null, null, "Macanese pataca", "$", "Prefix" },
                    { 91, "MRU", null, null, 2, "###,###,##0.00", true, null, null, "Mauritanian ouguiya", "UM", "Prefix" },
                    { 92, "MUR", null, null, 2, "###,###,##0.00", true, null, null, "Mauritian rupee", "₨", "Prefix" },
                    { 93, "MVR", null, null, 2, "###,###,##0.00", true, null, null, "Maldivian rufiyaa", "Rf", "Prefix" },
                    { 94, "MWK", null, null, 2, "###,###,##0.00", true, null, null, "Malawian kwacha", "MK", "Prefix" },
                    { 95, "MXN", null, null, 2, "###,###,##0.00", true, null, null, "Mexican peso", "$", "Prefix" },
                    { 96, "MYR", null, null, 2, "###,###,##0.00", true, null, null, "Malaysian ringgit", "RM", "Prefix" },
                    { 97, "MZN", null, null, 2, "###,###,##0.00", true, null, null, "Mozambican metical", "MT", "Prefix" },
                    { 98, "NAD", null, null, 2, "###,###,##0.00", true, null, null, "Namibian dollar", "$", "Prefix" },
                    { 99, "NGN", null, null, 2, "###,###,##0.00", true, null, null, "Nigerian naira", "₦", "Prefix" },
                    { 100, "NIO", null, null, 2, "###,###,##0.00", true, null, null, "Nicaraguan córdoba", "C$", "Prefix" },
                    { 101, "NOK", null, null, 2, "###,###,##0.00", true, null, null, "Norwegian krone", "ko", "Prefix" },
                    { 102, "NPR", null, null, 2, "##,##,##,##0.00", true, null, null, "Nepalese rupee", "₨", "Prefix" },
                    { 103, "NZD", null, null, 2, "###,###,##0.00", true, null, null, "New Zealand dollar", "$", "Prefix" },
                    { 104, "OMR", null, null, 3, "###,###,##0.000", true, null, null, "Omani rial", ".ع.ر", "Prefix" },
                    { 105, "PAB", null, null, 2, "###,###,##0.00", true, null, null, "Panamanian balboa", "B/.", "Prefix" },
                    { 106, "PEN", null, null, 2, "###,###,##0.00", true, null, null, "Peruvian sol", "S/.", "Prefix" },
                    { 107, "PGK", null, null, 2, "###,###,##0.00", true, null, null, "Papua New Guinean kina", "K", "Prefix" },
                    { 108, "PHP", null, null, 2, "###,###,##0.00", true, null, null, "Philippine peso", "₱", "Prefix" },
                    { 109, "PKR", null, null, 2, "##,##,##,##0.00", true, null, null, "Pakistani rupee", "₨", "Prefix" },
                    { 110, "PLN", null, null, 2, "###,###,##0.00", true, null, null, "Polish złoty", "zł", "Prefix" },
                    { 111, "PYG", null, null, 0, "###,###,##0", true, null, null, "Paraguayan guarani", "₲", "Prefix" },
                    { 112, "QAR", null, null, 2, "###,###,##0.00", true, null, null, "Qatari riyal", "ق.ر", "Prefix" },
                    { 113, "RON", null, null, 2, "###,###,##0.00", true, null, null, "Romanian leu", "lei", "Prefix" },
                    { 114, "RSD", null, null, 2, "###,###,##0.00", true, null, null, "Serbian dinar", "din", "Prefix" },
                    { 115, "RUB", null, null, 2, "###,###,##0.00", true, null, null, "Russian ruble", "₽", "Prefix" },
                    { 116, "RWF", null, null, 0, "###,###,##0", true, null, null, "Rwandan franc", "FRw", "Prefix" },
                    { 117, "SAR", null, null, 2, "###,###,##0.00", true, null, null, "Saudi riyal", "﷼", "Prefix" },
                    { 118, "SBD", null, null, 2, "###,###,##0.00", true, null, null, "Solomon Islands dollar", "Si$", "Prefix" },
                    { 119, "SCR", null, null, 2, "###,###,##0.00", true, null, null, "Seychellois rupee", "SRe", "Prefix" },
                    { 120, "SDG", null, null, 2, "###,###,##0.00", true, null, null, "Sudanese pound", ".س.ج", "Prefix" },
                    { 121, "SEK", null, null, 2, "###,###,##0.00", true, null, null, "Swedish krona", "ko", "Prefix" },
                    { 122, "SHP", null, null, 2, "###,###,##0.00", true, null, null, "Saint Helena pound", "£", "Prefix" },
                    { 123, "SLL", null, null, 2, "###,###,##0.00", true, null, null, "Sierra Leonean leone", "Le", "Prefix" },
                    { 124, "SOS", null, null, 2, "###,###,##0.00", true, null, null, "Somali shilling", "Sh.so.", "Prefix" },
                    { 125, "SRD", null, null, 2, "###,###,##0.00", true, null, null, "Surinamese dollar", "$", "Prefix" },
                    { 126, "SSP", null, null, 2, "###,###,##0.00", true, null, null, "South Sudanese pound", "£", "Prefix" },
                    { 127, "STN", null, null, 2, "###,###,##0.00", true, null, null, "Dobra", "Db", "Prefix" },
                    { 128, "SYP", null, null, 2, "###,###,##0.00", true, null, null, "Syrian pound", "LS", "Prefix" },
                    { 129, "SZL", null, null, 2, "###,###,##0.00", true, null, null, "Lilangeni", "E", "Prefix" },
                    { 130, "THB", null, null, 2, "###,###,##0.00", true, null, null, "Thai baht", "฿", "Prefix" },
                    { 131, "TJS", null, null, 2, "###,###,##0.00", true, null, null, "Tajikistani somoni", "SM", "Prefix" },
                    { 132, "TMT", null, null, 2, "###,###,##0.00", true, null, null, "Turkmenistan manat", "T", "Prefix" },
                    { 133, "TND", null, null, 3, "###,###,##0.000", true, null, null, "Tunisian dinar", "ت.د", "Prefix" },
                    { 134, "TOP", null, null, 2, "###,###,##0.00", true, null, null, "Tongan paʻanga", "$", "Prefix" },
                    { 135, "TRY", null, null, 2, "###,###,##0.00", true, null, null, "Turkish lira", "₺", "Prefix" },
                    { 136, "TTD", null, null, 2, "###,###,##0.00", true, null, null, "Trinidad and Tobago dollar", "$", "Prefix" },
                    { 137, "TWD", null, null, 2, "###,###,##0.00", true, null, null, "New Taiwan dollar", "$", "Prefix" },
                    { 138, "TZS", null, null, 2, "###,###,##0.00", true, null, null, "Tanzanian shilling", "TSh", "Prefix" },
                    { 139, "UAH", null, null, 2, "###,###,##0.00", true, null, null, "Ukrainian hryvnia", "₴", "Prefix" },
                    { 140, "UGX", null, null, 0, "###,###,##0", true, null, null, "Ugandan shilling", "USh", "Prefix" },
                    { 141, "UYU", null, null, 2, "###,###,##0.00", true, null, null, "Uruguayan peso", "$", "Prefix" },
                    { 142, "UZS", null, null, 2, "###,###,##0.00", true, null, null, "Uzbekistani soʻm", "лв", "Prefix" },
                    { 143, "VES", null, null, 2, "###,###,##0.00", true, null, null, "Bolívar", "Bs", "Prefix" },
                    { 144, "VND", null, null, 0, "###,###,##0", true, null, null, "Vietnamese đồng", "₫", "Prefix" },
                    { 145, "VUV", null, null, 0, "###,###,##0", true, null, null, "Vanuatu vatu", "VT", "Prefix" },
                    { 146, "WST", null, null, 2, "###,###,##0.00", true, null, null, "Samoan tālā", "SAT", "Prefix" },
                    { 147, "XAF", null, null, 0, "###,###,##0", true, null, null, "Central African CFA franc", "FCFA", "Prefix" },
                    { 148, "XCD", null, null, 2, "###,###,##0.00", true, null, null, "Eastern Caribbean dollar", "$", "Prefix" },
                    { 149, "XOF", null, null, 0, "###,###,##0", true, null, null, "West African CFA franc", "CFA", "Prefix" },
                    { 150, "XPF", null, null, 0, "###,###,##0", true, null, null, "CFP franc", "₣", "Prefix" },
                    { 151, "YER", null, null, 2, "###,###,##0.00", true, null, null, "Yemeni rial", "﷼", "Prefix" },
                    { 152, "ZAR", null, null, 2, "###,###,##0.00", true, null, null, "South African rand", "R", "Prefix" },
                    { 153, "ZMW", null, null, 2, "###,###,##0.00", true, null, null, "Zambian kwacha", "ZK", "Prefix" },
                    { 154, "ZWL", null, null, 2, "###,###,##0.00", true, null, null, "Zimbabwe Dollar", "$", "Prefix" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "HsnSacCodes",
                columns: new[] { "HsnSacCodeId", "ChapterCode", "Code", "CodeType", "CreatedAt", "CreatedBy", "DefaultGstRate", "Description", "DigitLength", "IsActive", "ModifiedAt", "ModifiedBy" },
                values: new object[,]
                {
                    { 1, "01", "01", "Hsn", null, null, null, "Live animals", (byte)2, true, null, null },
                    { 2, "02", "02", "Hsn", null, null, null, "Meat and edible meat offal", (byte)2, true, null, null },
                    { 3, "03", "03", "Hsn", null, null, null, "Fish and crustaceans, molluscs and other aquatic invertebrates", (byte)2, true, null, null },
                    { 4, "04", "04", "Hsn", null, null, null, "Dairy produce; birds' eggs; natural honey; edible products of animal origin", (byte)2, true, null, null },
                    { 5, "05", "05", "Hsn", null, null, null, "Products of animal origin, not elsewhere specified or included", (byte)2, true, null, null },
                    { 6, "06", "06", "Hsn", null, null, null, "Live trees and other plants; bulbs, roots; cut flowers and ornamental foliage", (byte)2, true, null, null },
                    { 7, "07", "07", "Hsn", null, null, null, "Edible vegetables and certain roots and tubers", (byte)2, true, null, null },
                    { 8, "08", "08", "Hsn", null, null, null, "Edible fruit and nuts; peel of citrus fruit or melons", (byte)2, true, null, null },
                    { 9, "09", "09", "Hsn", null, null, null, "Coffee, tea, mate and spices", (byte)2, true, null, null },
                    { 10, "10", "10", "Hsn", null, null, null, "Cereals", (byte)2, true, null, null },
                    { 11, "11", "11", "Hsn", null, null, null, "Products of the milling industry; malt; starches; inulin; wheat gluten", (byte)2, true, null, null },
                    { 12, "12", "12", "Hsn", null, null, null, "Oil seeds and oleaginous fruits; miscellaneous grains, seeds and fruit", (byte)2, true, null, null },
                    { 13, "13", "13", "Hsn", null, null, null, "Lac; gums, resins and other vegetable saps and extracts", (byte)2, true, null, null },
                    { 14, "14", "14", "Hsn", null, null, null, "Vegetable plaiting materials; vegetable products not elsewhere specified", (byte)2, true, null, null },
                    { 15, "15", "15", "Hsn", null, null, null, "Animal or vegetable fats and oils and their cleavage products", (byte)2, true, null, null },
                    { 16, "16", "16", "Hsn", null, null, null, "Preparations of meat, fish or crustaceans, molluscs or other aquatic invertebrates", (byte)2, true, null, null },
                    { 17, "17", "17", "Hsn", null, null, null, "Sugars and sugar confectionery", (byte)2, true, null, null },
                    { 18, "18", "18", "Hsn", null, null, null, "Cocoa and cocoa preparations", (byte)2, true, null, null },
                    { 19, "19", "19", "Hsn", null, null, null, "Preparations of cereals, flour, starch or milk; pastrycooks' products", (byte)2, true, null, null },
                    { 20, "20", "20", "Hsn", null, null, null, "Preparations of vegetables, fruit, nuts or other parts of plants", (byte)2, true, null, null },
                    { 21, "21", "21", "Hsn", null, null, null, "Miscellaneous edible preparations", (byte)2, true, null, null },
                    { 22, "22", "22", "Hsn", null, null, null, "Beverages, spirits and vinegar", (byte)2, true, null, null },
                    { 23, "23", "23", "Hsn", null, null, null, "Residues and waste from the food industries; prepared animal fodder", (byte)2, true, null, null },
                    { 24, "24", "24", "Hsn", null, null, null, "Tobacco and manufactured tobacco substitutes", (byte)2, true, null, null },
                    { 25, "25", "25", "Hsn", null, null, null, "Salt; sulphur; earths and stone; plastering materials, lime and cement", (byte)2, true, null, null },
                    { 26, "26", "26", "Hsn", null, null, null, "Ores, slag and ash", (byte)2, true, null, null },
                    { 27, "27", "27", "Hsn", null, null, null, "Mineral fuels, mineral oils and products of their distillation", (byte)2, true, null, null },
                    { 28, "28", "28", "Hsn", null, null, null, "Inorganic chemicals; compounds of precious metals and rare-earth metals", (byte)2, true, null, null },
                    { 29, "29", "29", "Hsn", null, null, null, "Organic chemicals", (byte)2, true, null, null },
                    { 30, "30", "30", "Hsn", null, null, null, "Pharmaceutical products", (byte)2, true, null, null },
                    { 31, "31", "31", "Hsn", null, null, null, "Fertilisers", (byte)2, true, null, null },
                    { 32, "32", "32", "Hsn", null, null, null, "Tanning or dyeing extracts; dyes, pigments, paints, varnishes, putty and inks", (byte)2, true, null, null },
                    { 33, "33", "33", "Hsn", null, null, null, "Essential oils and resinoids; perfumery, cosmetic or toilet preparations", (byte)2, true, null, null },
                    { 34, "34", "34", "Hsn", null, null, null, "Soap, organic surface-active agents, washing and lubricating preparations", (byte)2, true, null, null },
                    { 35, "35", "35", "Hsn", null, null, null, "Albuminoidal substances; modified starches; glues; enzymes", (byte)2, true, null, null },
                    { 36, "36", "36", "Hsn", null, null, null, "Explosives; pyrotechnic products; matches; certain combustible preparations", (byte)2, true, null, null },
                    { 37, "37", "37", "Hsn", null, null, null, "Photographic or cinematographic goods", (byte)2, true, null, null },
                    { 38, "38", "38", "Hsn", null, null, null, "Miscellaneous chemical products", (byte)2, true, null, null },
                    { 39, "39", "39", "Hsn", null, null, null, "Plastics and articles thereof", (byte)2, true, null, null },
                    { 40, "40", "40", "Hsn", null, null, null, "Rubber and articles thereof", (byte)2, true, null, null },
                    { 41, "41", "41", "Hsn", null, null, null, "Raw hides and skins (other than furskins) and leather", (byte)2, true, null, null },
                    { 42, "42", "42", "Hsn", null, null, null, "Articles of leather; saddlery and harness; travel goods, handbags", (byte)2, true, null, null },
                    { 43, "43", "43", "Hsn", null, null, null, "Furskins and artificial fur; manufactures thereof", (byte)2, true, null, null },
                    { 44, "44", "44", "Hsn", null, null, null, "Wood and articles of wood; wood charcoal", (byte)2, true, null, null },
                    { 45, "45", "45", "Hsn", null, null, null, "Cork and articles of cork", (byte)2, true, null, null },
                    { 46, "46", "46", "Hsn", null, null, null, "Manufactures of straw, esparto or other plaiting materials; basketware", (byte)2, true, null, null },
                    { 47, "47", "47", "Hsn", null, null, null, "Pulp of wood or other fibrous cellulosic material; recovered paper or paperboard", (byte)2, true, null, null },
                    { 48, "48", "48", "Hsn", null, null, null, "Paper and paperboard; articles of paper pulp, of paper or of paperboard", (byte)2, true, null, null },
                    { 49, "49", "49", "Hsn", null, null, null, "Printed books, newspapers, pictures and other products of the printing industry", (byte)2, true, null, null },
                    { 50, "50", "50", "Hsn", null, null, null, "Silk", (byte)2, true, null, null },
                    { 51, "51", "51", "Hsn", null, null, null, "Wool, fine or coarse animal hair; horsehair yarn and woven fabric", (byte)2, true, null, null },
                    { 52, "52", "52", "Hsn", null, null, null, "Cotton", (byte)2, true, null, null },
                    { 53, "53", "53", "Hsn", null, null, null, "Other vegetable textile fibres; paper yarn and woven fabric of paper yarn", (byte)2, true, null, null },
                    { 54, "54", "54", "Hsn", null, null, null, "Man-made filaments; strip and the like of man-made textile materials", (byte)2, true, null, null },
                    { 55, "55", "55", "Hsn", null, null, null, "Man-made staple fibres", (byte)2, true, null, null },
                    { 56, "56", "56", "Hsn", null, null, null, "Wadding, felt and nonwovens; special yarns; twine, cordage, ropes and cables", (byte)2, true, null, null },
                    { 57, "57", "57", "Hsn", null, null, null, "Carpets and other textile floor coverings", (byte)2, true, null, null },
                    { 58, "58", "58", "Hsn", null, null, null, "Special woven fabrics; tufted textile fabrics; lace; tapestries; embroidery", (byte)2, true, null, null },
                    { 59, "59", "59", "Hsn", null, null, null, "Impregnated, coated, covered or laminated textile fabrics", (byte)2, true, null, null },
                    { 60, "60", "60", "Hsn", null, null, null, "Knitted or crocheted fabrics", (byte)2, true, null, null },
                    { 61, "61", "61", "Hsn", null, null, null, "Articles of apparel and clothing accessories, knitted or crocheted", (byte)2, true, null, null },
                    { 62, "62", "62", "Hsn", null, null, null, "Articles of apparel and clothing accessories, not knitted or crocheted", (byte)2, true, null, null },
                    { 63, "63", "63", "Hsn", null, null, null, "Other made-up textile articles; sets; worn clothing and worn textile articles", (byte)2, true, null, null },
                    { 64, "64", "64", "Hsn", null, null, null, "Footwear, gaiters and the like; parts of such articles", (byte)2, true, null, null },
                    { 65, "65", "65", "Hsn", null, null, null, "Headgear and parts thereof", (byte)2, true, null, null },
                    { 66, "66", "66", "Hsn", null, null, null, "Umbrellas, sun umbrellas, walking sticks, whips, riding crops and parts", (byte)2, true, null, null },
                    { 67, "67", "67", "Hsn", null, null, null, "Prepared feathers and down; artificial flowers; articles of human hair", (byte)2, true, null, null },
                    { 68, "68", "68", "Hsn", null, null, null, "Articles of stone, plaster, cement, asbestos, mica or similar materials", (byte)2, true, null, null },
                    { 69, "69", "69", "Hsn", null, null, null, "Ceramic products", (byte)2, true, null, null },
                    { 70, "70", "70", "Hsn", null, null, null, "Glass and glassware", (byte)2, true, null, null },
                    { 71, "71", "71", "Hsn", null, null, null, "Natural or cultured pearls, precious stones, precious metals; imitation jewellery", (byte)2, true, null, null },
                    { 72, "72", "72", "Hsn", null, null, null, "Iron and steel", (byte)2, true, null, null },
                    { 73, "73", "73", "Hsn", null, null, null, "Articles of iron or steel", (byte)2, true, null, null },
                    { 74, "74", "74", "Hsn", null, null, null, "Copper and articles thereof", (byte)2, true, null, null },
                    { 75, "75", "75", "Hsn", null, null, null, "Nickel and articles thereof", (byte)2, true, null, null },
                    { 76, "76", "76", "Hsn", null, null, null, "Aluminium and articles thereof", (byte)2, true, null, null },
                    { 77, "78", "78", "Hsn", null, null, null, "Lead and articles thereof", (byte)2, true, null, null },
                    { 78, "79", "79", "Hsn", null, null, null, "Zinc and articles thereof", (byte)2, true, null, null },
                    { 79, "80", "80", "Hsn", null, null, null, "Tin and articles thereof", (byte)2, true, null, null },
                    { 80, "81", "81", "Hsn", null, null, null, "Other base metals; cermets; articles thereof", (byte)2, true, null, null },
                    { 81, "82", "82", "Hsn", null, null, null, "Tools, implements, cutlery, spoons and forks, of base metal", (byte)2, true, null, null },
                    { 82, "83", "83", "Hsn", null, null, null, "Miscellaneous articles of base metal", (byte)2, true, null, null },
                    { 83, "84", "84", "Hsn", null, null, null, "Nuclear reactors, boilers, machinery and mechanical appliances; parts thereof", (byte)2, true, null, null },
                    { 84, "85", "85", "Hsn", null, null, null, "Electrical machinery and equipment and parts thereof; sound and TV apparatus", (byte)2, true, null, null },
                    { 85, "86", "86", "Hsn", null, null, null, "Railway or tramway locomotives, rolling stock and parts; track fixtures", (byte)2, true, null, null },
                    { 86, "87", "87", "Hsn", null, null, null, "Vehicles other than railway or tramway rolling stock, and parts thereof", (byte)2, true, null, null },
                    { 87, "88", "88", "Hsn", null, null, null, "Aircraft, spacecraft, and parts thereof", (byte)2, true, null, null },
                    { 88, "89", "89", "Hsn", null, null, null, "Ships, boats and floating structures", (byte)2, true, null, null },
                    { 89, "90", "90", "Hsn", null, null, null, "Optical, photographic, measuring, checking, precision and medical instruments", (byte)2, true, null, null },
                    { 90, "91", "91", "Hsn", null, null, null, "Clocks and watches and parts thereof", (byte)2, true, null, null },
                    { 91, "92", "92", "Hsn", null, null, null, "Musical instruments; parts and accessories of such articles", (byte)2, true, null, null },
                    { 92, "93", "93", "Hsn", null, null, null, "Arms and ammunition; parts and accessories thereof", (byte)2, true, null, null },
                    { 93, "94", "94", "Hsn", null, null, null, "Furniture; bedding, mattresses, cushions; lamps and lighting fittings", (byte)2, true, null, null },
                    { 94, "95", "95", "Hsn", null, null, null, "Toys, games and sports requisites; parts and accessories thereof", (byte)2, true, null, null },
                    { 95, "96", "96", "Hsn", null, null, null, "Miscellaneous manufactured articles", (byte)2, true, null, null },
                    { 96, "97", "97", "Hsn", null, null, null, "Works of art, collectors' pieces and antiques", (byte)2, true, null, null },
                    { 97, "98", "98", "Hsn", null, null, null, "Project imports; laboratory chemicals; passengers' baggage", (byte)2, true, null, null },
                    { 98, "99", "99", "Sac", null, null, null, "Services", (byte)2, true, null, null },
                    { 99, "99", "9954", "Sac", null, null, null, "Construction services", (byte)4, true, null, null },
                    { 100, "99", "9961", "Sac", null, null, null, "Services in wholesale trade", (byte)4, true, null, null },
                    { 101, "99", "9962", "Sac", null, null, null, "Services in retail trade", (byte)4, true, null, null },
                    { 102, "99", "9963", "Sac", null, null, null, "Accommodation, food and beverage services", (byte)4, true, null, null },
                    { 103, "99", "9964", "Sac", null, null, null, "Passenger transport services", (byte)4, true, null, null },
                    { 104, "99", "9965", "Sac", null, null, null, "Goods transport services", (byte)4, true, null, null },
                    { 105, "99", "9966", "Sac", null, null, null, "Rental services of transport vehicles with operators", (byte)4, true, null, null },
                    { 106, "99", "9967", "Sac", null, null, null, "Supporting services in transport", (byte)4, true, null, null },
                    { 107, "99", "9968", "Sac", null, null, null, "Postal and courier services", (byte)4, true, null, null },
                    { 108, "99", "9969", "Sac", null, null, null, "Electricity, gas, water and other distribution services", (byte)4, true, null, null },
                    { 109, "99", "9971", "Sac", null, null, null, "Financial and related services", (byte)4, true, null, null },
                    { 110, "99", "9972", "Sac", null, null, null, "Real estate services", (byte)4, true, null, null },
                    { 111, "99", "9973", "Sac", null, null, null, "Leasing or rental services without operator", (byte)4, true, null, null },
                    { 112, "99", "9981", "Sac", null, null, null, "Research and development services", (byte)4, true, null, null },
                    { 113, "99", "9982", "Sac", null, null, null, "Legal and accounting services", (byte)4, true, null, null },
                    { 114, "99", "9983", "Sac", null, null, null, "Other professional, technical and business services", (byte)4, true, null, null },
                    { 115, "99", "9984", "Sac", null, null, null, "Telecommunications, broadcasting and information supply services", (byte)4, true, null, null },
                    { 116, "99", "9985", "Sac", null, null, null, "Support services", (byte)4, true, null, null },
                    { 117, "99", "9986", "Sac", null, null, null, "Support services to agriculture, hunting, forestry, fishing and mining", (byte)4, true, null, null },
                    { 118, "99", "9987", "Sac", null, null, null, "Maintenance, repair and installation services", (byte)4, true, null, null },
                    { 119, "99", "9988", "Sac", null, null, null, "Manufacturing services on physical inputs owned by others", (byte)4, true, null, null },
                    { 120, "99", "9989", "Sac", null, null, null, "Other manufacturing services; publishing, printing and reproduction", (byte)4, true, null, null },
                    { 121, "99", "9991", "Sac", null, null, null, "Public administration and other services to the community", (byte)4, true, null, null },
                    { 122, "99", "9992", "Sac", null, null, null, "Education services", (byte)4, true, null, null },
                    { 123, "99", "9993", "Sac", null, null, null, "Human health and social care services", (byte)4, true, null, null },
                    { 124, "99", "9994", "Sac", null, null, null, "Sewage and waste collection, treatment and disposal services", (byte)4, true, null, null },
                    { 125, "99", "9995", "Sac", null, null, null, "Services of membership organisations", (byte)4, true, null, null },
                    { 126, "99", "9996", "Sac", null, null, null, "Recreational, cultural and sporting services", (byte)4, true, null, null },
                    { 127, "99", "9997", "Sac", null, null, null, "Other services", (byte)4, true, null, null },
                    { 128, "99", "9998", "Sac", null, null, null, "Domestic services", (byte)4, true, null, null },
                    { 129, "99", "9999", "Sac", null, null, null, "Services provided by extraterritorial organisations and bodies", (byte)4, true, null, null }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "LedgerSources",
                columns: new[] { "LedgerSourceId", "Code", "CreatedAt", "CreatedBy", "Direction", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, "TRANSACTION", null, null, "Both", true, null, null, "Document posting" },
                    { 2, "BILLPAYMENT", null, null, "Out", true, null, null, "Bill payment" },
                    { 3, "INVOICEPAYMENT", null, null, "In", true, null, null, "Invoice payment" },
                    { 4, "BILLREFUND", null, null, "In", true, null, null, "Bill refund received" },
                    { 5, "INVOICEREFUND", null, null, "Out", true, null, null, "Invoice refund paid" },
                    { 6, "CREDITNOTEREFUND", null, null, "Out", true, null, null, "Credit note refund paid" },
                    { 7, "DEBITNOTEREFUND", null, null, "In", true, null, null, "Debit note refund received" },
                    { 8, "VENDORPREPAYMENT", null, null, "Out", true, null, null, "Advance paid to vendor" },
                    { 9, "CUSTOMERPREPAYMENT", null, null, "In", true, null, null, "Advance received from customer" },
                    { 10, "ALLOCATION", null, null, "Both", true, null, null, "Credit note, debit note or prepayment allocation" },
                    { 11, "MONEYTRANSFER", null, null, "Both", true, null, null, "Bank or cash transfer" },
                    { 12, "JOURNAL", null, null, "Both", true, null, null, "Manual journal" },
                    { 13, "OPENINGBALANCE", null, null, "Both", true, null, null, "Opening balance" },
                    { 14, "DEPRECIATION", null, null, "Out", true, null, null, "Depreciation" },
                    { 15, "STOCKADJUSTMENT", null, null, "Both", true, null, null, "Stock adjustment" },
                    { 16, "VENDOROVERPAYMENT", null, null, "Out", true, null, null, "Overpayment to vendor" },
                    { 17, "CUSTOMEROVERPAYMENT", null, null, "In", true, null, null, "Overpayment from customer" },
                    { 18, "CUSTOMEROVERPAYMENTREFUND", null, null, "Out", true, null, null, "Customer overpayment refunded" },
                    { 19, "CUSTOMERPREPAYMENTREFUND", null, null, "Out", true, null, null, "Customer advance refunded" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "LedgerTypes",
                columns: new[] { "LedgerTypeId", "Code", "CreatedAt", "CreatedBy", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, "ITEM", null, null, true, null, null, "Line item" },
                    { 2, "TAX", null, null, true, null, null, "Tax" },
                    { 3, "CONTROL", null, null, true, null, null, "AP / AR / bank / cash control leg" },
                    { 4, "COGS", null, null, true, null, null, "Cost of goods sold" },
                    { 5, "FX", null, null, true, null, null, "Realized exchange gain or loss" },
                    { 6, "ROUNDOFF", null, null, true, null, null, "Rounding" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { 1, "dashboard", null, null, 1, "home", true, null, null, "Dashboard" },
                    { 2, "sales", null, null, 2, "shopping_cart", true, null, null, "Sales" },
                    { 3, "purchase", null, null, 3, "inventory", true, null, null, "Purchase" },
                    { 4, "inventory", null, null, 4, "warehouse", true, null, null, "Inventory" },
                    { 5, "accounting", null, null, 5, "account_balance", true, null, null, "Accounts" },
                    { 6, "banking", null, null, 6, "account_balance_wallet", true, null, null, "Banking" },
                    { 7, "contacts", null, null, 7, "people", true, null, null, "Contacts" },
                    { 8, "reports", null, null, 8, "bar_chart", true, null, null, "Reports" },
                    { 9, "settings", null, null, 9, "settings", true, null, null, "Settings" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Permissions",
                columns: new[] { "PermissionId", "Code", "CreatedAt", "CreatedBy", "Description", "ModifiedAt", "ModifiedBy", "Module" },
                values: new object[,]
                {
                    { 1, "dashboard.view", null, null, null, null, null, "dashboard" },
                    { 2, "dashboard.create", null, null, null, null, null, "dashboard" },
                    { 3, "dashboard.edit", null, null, null, null, null, "dashboard" },
                    { 4, "dashboard.approve", null, null, null, null, null, "dashboard" },
                    { 5, "dashboard.void", null, null, null, null, null, "dashboard" },
                    { 6, "dashboard.delete", null, null, null, null, null, "dashboard" },
                    { 7, "dashboard.print", null, null, null, null, null, "dashboard" },
                    { 8, "dashboard.export", null, null, null, null, null, "dashboard" },
                    { 9, "dashboard.import", null, null, null, null, null, "dashboard" },
                    { 10, "dashboard.AllUserData", null, null, null, null, null, "dashboard" },
                    { 11, "contacts.view", null, null, null, null, null, "contacts" },
                    { 12, "contacts.create", null, null, null, null, null, "contacts" },
                    { 13, "contacts.edit", null, null, null, null, null, "contacts" },
                    { 14, "contacts.approve", null, null, null, null, null, "contacts" },
                    { 15, "contacts.void", null, null, null, null, null, "contacts" },
                    { 16, "contacts.delete", null, null, null, null, null, "contacts" },
                    { 17, "contacts.print", null, null, null, null, null, "contacts" },
                    { 18, "contacts.export", null, null, null, null, null, "contacts" },
                    { 19, "contacts.import", null, null, null, null, null, "contacts" },
                    { 20, "contacts.AllUserData", null, null, null, null, null, "contacts" },
                    { 21, "crm.view", null, null, null, null, null, "crm" },
                    { 22, "crm.create", null, null, null, null, null, "crm" },
                    { 23, "crm.edit", null, null, null, null, null, "crm" },
                    { 24, "crm.approve", null, null, null, null, null, "crm" },
                    { 25, "crm.void", null, null, null, null, null, "crm" },
                    { 26, "crm.delete", null, null, null, null, null, "crm" },
                    { 27, "crm.print", null, null, null, null, null, "crm" },
                    { 28, "crm.export", null, null, null, null, null, "crm" },
                    { 29, "crm.import", null, null, null, null, null, "crm" },
                    { 30, "crm.AllUserData", null, null, null, null, null, "crm" },
                    { 31, "inventory.view", null, null, null, null, null, "inventory" },
                    { 32, "inventory.create", null, null, null, null, null, "inventory" },
                    { 33, "inventory.edit", null, null, null, null, null, "inventory" },
                    { 34, "inventory.approve", null, null, null, null, null, "inventory" },
                    { 35, "inventory.void", null, null, null, null, null, "inventory" },
                    { 36, "inventory.delete", null, null, null, null, null, "inventory" },
                    { 37, "inventory.print", null, null, null, null, null, "inventory" },
                    { 38, "inventory.export", null, null, null, null, null, "inventory" },
                    { 39, "inventory.import", null, null, null, null, null, "inventory" },
                    { 40, "inventory.AllUserData", null, null, null, null, null, "inventory" },
                    { 41, "sales.view", null, null, null, null, null, "sales" },
                    { 42, "sales.create", null, null, null, null, null, "sales" },
                    { 43, "sales.edit", null, null, null, null, null, "sales" },
                    { 44, "sales.approve", null, null, null, null, null, "sales" },
                    { 45, "sales.void", null, null, null, null, null, "sales" },
                    { 46, "sales.delete", null, null, null, null, null, "sales" },
                    { 47, "sales.print", null, null, null, null, null, "sales" },
                    { 48, "sales.export", null, null, null, null, null, "sales" },
                    { 49, "sales.import", null, null, null, null, null, "sales" },
                    { 50, "sales.AllUserData", null, null, null, null, null, "sales" },
                    { 51, "purchase.view", null, null, null, null, null, "purchase" },
                    { 52, "purchase.create", null, null, null, null, null, "purchase" },
                    { 53, "purchase.edit", null, null, null, null, null, "purchase" },
                    { 54, "purchase.approve", null, null, null, null, null, "purchase" },
                    { 55, "purchase.void", null, null, null, null, null, "purchase" },
                    { 56, "purchase.delete", null, null, null, null, null, "purchase" },
                    { 57, "purchase.print", null, null, null, null, null, "purchase" },
                    { 58, "purchase.export", null, null, null, null, null, "purchase" },
                    { 59, "purchase.import", null, null, null, null, null, "purchase" },
                    { 60, "purchase.AllUserData", null, null, null, null, null, "purchase" },
                    { 61, "accounting.view", null, null, null, null, null, "accounting" },
                    { 62, "accounting.create", null, null, null, null, null, "accounting" },
                    { 63, "accounting.edit", null, null, null, null, null, "accounting" },
                    { 64, "accounting.approve", null, null, null, null, null, "accounting" },
                    { 65, "accounting.void", null, null, null, null, null, "accounting" },
                    { 66, "accounting.delete", null, null, null, null, null, "accounting" },
                    { 67, "accounting.print", null, null, null, null, null, "accounting" },
                    { 68, "accounting.export", null, null, null, null, null, "accounting" },
                    { 69, "accounting.import", null, null, null, null, null, "accounting" },
                    { 70, "accounting.AllUserData", null, null, null, null, null, "accounting" },
                    { 71, "banking.view", null, null, null, null, null, "banking" },
                    { 72, "banking.create", null, null, null, null, null, "banking" },
                    { 73, "banking.edit", null, null, null, null, null, "banking" },
                    { 74, "banking.approve", null, null, null, null, null, "banking" },
                    { 75, "banking.void", null, null, null, null, null, "banking" },
                    { 76, "banking.delete", null, null, null, null, null, "banking" },
                    { 77, "banking.print", null, null, null, null, null, "banking" },
                    { 78, "banking.export", null, null, null, null, null, "banking" },
                    { 79, "banking.import", null, null, null, null, null, "banking" },
                    { 80, "banking.AllUserData", null, null, null, null, null, "banking" },
                    { 81, "reports.view", null, null, null, null, null, "reports" },
                    { 82, "reports.create", null, null, null, null, null, "reports" },
                    { 83, "reports.edit", null, null, null, null, null, "reports" },
                    { 84, "reports.approve", null, null, null, null, null, "reports" },
                    { 85, "reports.void", null, null, null, null, null, "reports" },
                    { 86, "reports.delete", null, null, null, null, null, "reports" },
                    { 87, "reports.print", null, null, null, null, null, "reports" },
                    { 88, "reports.export", null, null, null, null, null, "reports" },
                    { 89, "reports.import", null, null, null, null, null, "reports" },
                    { 90, "reports.AllUserData", null, null, null, null, null, "reports" },
                    { 91, "settings.view", null, null, null, null, null, "settings" },
                    { 92, "settings.create", null, null, null, null, null, "settings" },
                    { 93, "settings.edit", null, null, null, null, null, "settings" },
                    { 94, "settings.approve", null, null, null, null, null, "settings" },
                    { 95, "settings.void", null, null, null, null, null, "settings" },
                    { 96, "settings.delete", null, null, null, null, null, "settings" },
                    { 97, "settings.print", null, null, null, null, null, "settings" },
                    { 98, "settings.export", null, null, null, null, null, "settings" },
                    { 99, "settings.import", null, null, null, null, null, "settings" },
                    { 100, "settings.AllUserData", null, null, null, null, null, "settings" },
                    { 101, "support.view", null, null, null, null, null, "support" },
                    { 102, "support.create", null, null, null, null, null, "support" },
                    { 103, "support.edit", null, null, null, null, null, "support" },
                    { 104, "support.approve", null, null, null, null, null, "support" },
                    { 105, "support.void", null, null, null, null, null, "support" },
                    { 106, "support.delete", null, null, null, null, null, "support" },
                    { 107, "support.print", null, null, null, null, null, "support" },
                    { 108, "support.export", null, null, null, null, null, "support" },
                    { 109, "support.import", null, null, null, null, null, "support" },
                    { 110, "support.AllUserData", null, null, null, null, null, "support" },
                    { 111, "platform.view", null, null, null, null, null, "platform" },
                    { 112, "platform.create", null, null, null, null, null, "platform" },
                    { 113, "platform.edit", null, null, null, null, null, "platform" },
                    { 114, "platform.approve", null, null, null, null, null, "platform" },
                    { 115, "platform.void", null, null, null, null, null, "platform" },
                    { 116, "platform.delete", null, null, null, null, null, "platform" },
                    { 117, "platform.print", null, null, null, null, null, "platform" },
                    { 118, "platform.export", null, null, null, null, null, "platform" },
                    { 119, "platform.import", null, null, null, null, null, "platform" },
                    { 120, "platform.AllUserData", null, null, null, null, null, "platform" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1L, null, null, null, null, 1, 1 },
                    { 2L, null, null, null, null, 2, 1 },
                    { 3L, null, null, null, null, 3, 1 },
                    { 4L, null, null, null, null, 4, 1 },
                    { 5L, null, null, null, null, 5, 1 },
                    { 6L, null, null, null, null, 6, 1 },
                    { 7L, null, null, null, null, 7, 1 },
                    { 8L, null, null, null, null, 8, 1 },
                    { 9L, null, null, null, null, 9, 1 },
                    { 10L, null, null, null, null, 10, 1 },
                    { 11L, null, null, null, null, 11, 1 },
                    { 12L, null, null, null, null, 12, 1 },
                    { 13L, null, null, null, null, 13, 1 },
                    { 14L, null, null, null, null, 14, 1 },
                    { 15L, null, null, null, null, 15, 1 },
                    { 16L, null, null, null, null, 16, 1 },
                    { 17L, null, null, null, null, 17, 1 },
                    { 18L, null, null, null, null, 18, 1 },
                    { 19L, null, null, null, null, 19, 1 },
                    { 20L, null, null, null, null, 20, 1 },
                    { 21L, null, null, null, null, 21, 1 },
                    { 22L, null, null, null, null, 22, 1 },
                    { 23L, null, null, null, null, 23, 1 },
                    { 24L, null, null, null, null, 24, 1 },
                    { 25L, null, null, null, null, 25, 1 },
                    { 26L, null, null, null, null, 26, 1 },
                    { 27L, null, null, null, null, 27, 1 },
                    { 28L, null, null, null, null, 28, 1 },
                    { 29L, null, null, null, null, 29, 1 },
                    { 30L, null, null, null, null, 30, 1 },
                    { 31L, null, null, null, null, 31, 1 },
                    { 32L, null, null, null, null, 32, 1 },
                    { 33L, null, null, null, null, 33, 1 },
                    { 34L, null, null, null, null, 34, 1 },
                    { 35L, null, null, null, null, 35, 1 },
                    { 36L, null, null, null, null, 36, 1 },
                    { 37L, null, null, null, null, 37, 1 },
                    { 38L, null, null, null, null, 38, 1 },
                    { 39L, null, null, null, null, 39, 1 },
                    { 40L, null, null, null, null, 40, 1 },
                    { 41L, null, null, null, null, 41, 1 },
                    { 42L, null, null, null, null, 42, 1 },
                    { 43L, null, null, null, null, 43, 1 },
                    { 44L, null, null, null, null, 44, 1 },
                    { 45L, null, null, null, null, 45, 1 },
                    { 46L, null, null, null, null, 46, 1 },
                    { 47L, null, null, null, null, 47, 1 },
                    { 48L, null, null, null, null, 48, 1 },
                    { 49L, null, null, null, null, 49, 1 },
                    { 50L, null, null, null, null, 50, 1 },
                    { 51L, null, null, null, null, 51, 1 },
                    { 52L, null, null, null, null, 52, 1 },
                    { 53L, null, null, null, null, 53, 1 },
                    { 54L, null, null, null, null, 54, 1 },
                    { 55L, null, null, null, null, 55, 1 },
                    { 56L, null, null, null, null, 56, 1 },
                    { 57L, null, null, null, null, 57, 1 },
                    { 58L, null, null, null, null, 58, 1 },
                    { 59L, null, null, null, null, 59, 1 },
                    { 60L, null, null, null, null, 60, 1 },
                    { 61L, null, null, null, null, 61, 1 },
                    { 62L, null, null, null, null, 62, 1 },
                    { 63L, null, null, null, null, 63, 1 },
                    { 64L, null, null, null, null, 64, 1 },
                    { 65L, null, null, null, null, 65, 1 },
                    { 66L, null, null, null, null, 66, 1 },
                    { 67L, null, null, null, null, 67, 1 },
                    { 68L, null, null, null, null, 68, 1 },
                    { 69L, null, null, null, null, 69, 1 },
                    { 70L, null, null, null, null, 70, 1 },
                    { 71L, null, null, null, null, 71, 1 },
                    { 72L, null, null, null, null, 72, 1 },
                    { 73L, null, null, null, null, 73, 1 },
                    { 74L, null, null, null, null, 74, 1 },
                    { 75L, null, null, null, null, 75, 1 },
                    { 76L, null, null, null, null, 76, 1 },
                    { 77L, null, null, null, null, 77, 1 },
                    { 78L, null, null, null, null, 78, 1 },
                    { 79L, null, null, null, null, 79, 1 },
                    { 80L, null, null, null, null, 80, 1 },
                    { 81L, null, null, null, null, 81, 1 },
                    { 82L, null, null, null, null, 82, 1 },
                    { 83L, null, null, null, null, 83, 1 },
                    { 84L, null, null, null, null, 84, 1 },
                    { 85L, null, null, null, null, 85, 1 },
                    { 86L, null, null, null, null, 86, 1 },
                    { 87L, null, null, null, null, 87, 1 },
                    { 88L, null, null, null, null, 88, 1 },
                    { 89L, null, null, null, null, 89, 1 },
                    { 90L, null, null, null, null, 90, 1 },
                    { 91L, null, null, null, null, 91, 1 },
                    { 92L, null, null, null, null, 92, 1 },
                    { 93L, null, null, null, null, 93, 1 },
                    { 94L, null, null, null, null, 94, 1 },
                    { 95L, null, null, null, null, 95, 1 },
                    { 96L, null, null, null, null, 96, 1 },
                    { 97L, null, null, null, null, 97, 1 },
                    { 98L, null, null, null, null, 98, 1 },
                    { 99L, null, null, null, null, 99, 1 },
                    { 100L, null, null, null, null, 100, 1 },
                    { 101L, null, null, null, null, 101, 1 },
                    { 102L, null, null, null, null, 102, 1 },
                    { 103L, null, null, null, null, 103, 1 },
                    { 104L, null, null, null, null, 104, 1 },
                    { 105L, null, null, null, null, 105, 1 },
                    { 106L, null, null, null, null, 106, 1 },
                    { 107L, null, null, null, null, 107, 1 },
                    { 108L, null, null, null, null, 108, 1 },
                    { 109L, null, null, null, null, 109, 1 },
                    { 110L, null, null, null, null, 110, 1 },
                    { 111L, null, null, null, null, 1, 2 },
                    { 112L, null, null, null, null, 2, 2 },
                    { 113L, null, null, null, null, 3, 2 },
                    { 114L, null, null, null, null, 4, 2 },
                    { 115L, null, null, null, null, 5, 2 },
                    { 116L, null, null, null, null, 6, 2 },
                    { 117L, null, null, null, null, 7, 2 },
                    { 118L, null, null, null, null, 8, 2 },
                    { 119L, null, null, null, null, 9, 2 },
                    { 120L, null, null, null, null, 10, 2 },
                    { 121L, null, null, null, null, 11, 2 },
                    { 122L, null, null, null, null, 12, 2 },
                    { 123L, null, null, null, null, 13, 2 },
                    { 124L, null, null, null, null, 14, 2 },
                    { 125L, null, null, null, null, 15, 2 },
                    { 126L, null, null, null, null, 16, 2 },
                    { 127L, null, null, null, null, 17, 2 },
                    { 128L, null, null, null, null, 18, 2 },
                    { 129L, null, null, null, null, 19, 2 },
                    { 130L, null, null, null, null, 20, 2 },
                    { 131L, null, null, null, null, 21, 2 },
                    { 132L, null, null, null, null, 22, 2 },
                    { 133L, null, null, null, null, 23, 2 },
                    { 134L, null, null, null, null, 24, 2 },
                    { 135L, null, null, null, null, 25, 2 },
                    { 136L, null, null, null, null, 26, 2 },
                    { 137L, null, null, null, null, 27, 2 },
                    { 138L, null, null, null, null, 28, 2 },
                    { 139L, null, null, null, null, 29, 2 },
                    { 140L, null, null, null, null, 30, 2 },
                    { 141L, null, null, null, null, 31, 2 },
                    { 142L, null, null, null, null, 32, 2 },
                    { 143L, null, null, null, null, 33, 2 },
                    { 144L, null, null, null, null, 34, 2 },
                    { 145L, null, null, null, null, 35, 2 },
                    { 146L, null, null, null, null, 36, 2 },
                    { 147L, null, null, null, null, 37, 2 },
                    { 148L, null, null, null, null, 38, 2 },
                    { 149L, null, null, null, null, 39, 2 },
                    { 150L, null, null, null, null, 40, 2 },
                    { 151L, null, null, null, null, 41, 2 },
                    { 152L, null, null, null, null, 42, 2 },
                    { 153L, null, null, null, null, 43, 2 },
                    { 154L, null, null, null, null, 44, 2 },
                    { 155L, null, null, null, null, 45, 2 },
                    { 156L, null, null, null, null, 46, 2 },
                    { 157L, null, null, null, null, 47, 2 },
                    { 158L, null, null, null, null, 48, 2 },
                    { 159L, null, null, null, null, 49, 2 },
                    { 160L, null, null, null, null, 50, 2 },
                    { 161L, null, null, null, null, 51, 2 },
                    { 162L, null, null, null, null, 52, 2 },
                    { 163L, null, null, null, null, 53, 2 },
                    { 164L, null, null, null, null, 54, 2 },
                    { 165L, null, null, null, null, 55, 2 },
                    { 166L, null, null, null, null, 56, 2 },
                    { 167L, null, null, null, null, 57, 2 },
                    { 168L, null, null, null, null, 58, 2 },
                    { 169L, null, null, null, null, 59, 2 },
                    { 170L, null, null, null, null, 60, 2 },
                    { 171L, null, null, null, null, 61, 2 },
                    { 172L, null, null, null, null, 62, 2 },
                    { 173L, null, null, null, null, 63, 2 },
                    { 174L, null, null, null, null, 64, 2 },
                    { 175L, null, null, null, null, 65, 2 },
                    { 176L, null, null, null, null, 66, 2 },
                    { 177L, null, null, null, null, 67, 2 },
                    { 178L, null, null, null, null, 68, 2 },
                    { 179L, null, null, null, null, 69, 2 },
                    { 180L, null, null, null, null, 70, 2 },
                    { 181L, null, null, null, null, 71, 2 },
                    { 182L, null, null, null, null, 72, 2 },
                    { 183L, null, null, null, null, 73, 2 },
                    { 184L, null, null, null, null, 74, 2 },
                    { 185L, null, null, null, null, 75, 2 },
                    { 186L, null, null, null, null, 76, 2 },
                    { 187L, null, null, null, null, 77, 2 },
                    { 188L, null, null, null, null, 78, 2 },
                    { 189L, null, null, null, null, 79, 2 },
                    { 190L, null, null, null, null, 80, 2 },
                    { 191L, null, null, null, null, 81, 2 },
                    { 192L, null, null, null, null, 82, 2 },
                    { 193L, null, null, null, null, 83, 2 },
                    { 194L, null, null, null, null, 84, 2 },
                    { 195L, null, null, null, null, 85, 2 },
                    { 196L, null, null, null, null, 86, 2 },
                    { 197L, null, null, null, null, 87, 2 },
                    { 198L, null, null, null, null, 88, 2 },
                    { 199L, null, null, null, null, 89, 2 },
                    { 200L, null, null, null, null, 90, 2 },
                    { 201L, null, null, null, null, 91, 2 },
                    { 202L, null, null, null, null, 92, 2 },
                    { 203L, null, null, null, null, 93, 2 },
                    { 204L, null, null, null, null, 94, 2 },
                    { 205L, null, null, null, null, 95, 2 },
                    { 206L, null, null, null, null, 96, 2 },
                    { 207L, null, null, null, null, 97, 2 },
                    { 208L, null, null, null, null, 98, 2 },
                    { 209L, null, null, null, null, 99, 2 },
                    { 210L, null, null, null, null, 100, 2 },
                    { 211L, null, null, null, null, 101, 2 },
                    { 212L, null, null, null, null, 102, 2 },
                    { 213L, null, null, null, null, 103, 2 },
                    { 214L, null, null, null, null, 104, 2 },
                    { 215L, null, null, null, null, 105, 2 },
                    { 216L, null, null, null, null, 106, 2 },
                    { 217L, null, null, null, null, 107, 2 },
                    { 218L, null, null, null, null, 108, 2 },
                    { 219L, null, null, null, null, 109, 2 },
                    { 220L, null, null, null, null, 110, 2 },
                    { 221L, null, null, null, null, 51, 3 },
                    { 222L, null, null, null, null, 52, 3 },
                    { 223L, null, null, null, null, 53, 3 },
                    { 224L, null, null, null, null, 54, 3 },
                    { 225L, null, null, null, null, 55, 3 },
                    { 226L, null, null, null, null, 56, 3 },
                    { 227L, null, null, null, null, 57, 3 },
                    { 228L, null, null, null, null, 58, 3 },
                    { 229L, null, null, null, null, 59, 3 },
                    { 230L, null, null, null, null, 60, 3 },
                    { 231L, null, null, null, null, 61, 3 },
                    { 232L, null, null, null, null, 62, 3 },
                    { 233L, null, null, null, null, 63, 3 },
                    { 234L, null, null, null, null, 64, 3 },
                    { 235L, null, null, null, null, 65, 3 },
                    { 236L, null, null, null, null, 66, 3 },
                    { 237L, null, null, null, null, 67, 3 },
                    { 238L, null, null, null, null, 68, 3 },
                    { 239L, null, null, null, null, 69, 3 },
                    { 240L, null, null, null, null, 70, 3 },
                    { 241L, null, null, null, null, 71, 3 },
                    { 242L, null, null, null, null, 72, 3 },
                    { 243L, null, null, null, null, 73, 3 },
                    { 244L, null, null, null, null, 74, 3 },
                    { 245L, null, null, null, null, 75, 3 },
                    { 246L, null, null, null, null, 76, 3 },
                    { 247L, null, null, null, null, 77, 3 },
                    { 248L, null, null, null, null, 78, 3 },
                    { 249L, null, null, null, null, 79, 3 },
                    { 250L, null, null, null, null, 80, 3 },
                    { 251L, null, null, null, null, 81, 3 },
                    { 252L, null, null, null, null, 82, 3 },
                    { 253L, null, null, null, null, 83, 3 },
                    { 254L, null, null, null, null, 84, 3 },
                    { 255L, null, null, null, null, 85, 3 },
                    { 256L, null, null, null, null, 86, 3 },
                    { 257L, null, null, null, null, 87, 3 },
                    { 258L, null, null, null, null, 88, 3 },
                    { 259L, null, null, null, null, 89, 3 },
                    { 260L, null, null, null, null, 90, 3 },
                    { 261L, null, null, null, null, 11, 4 },
                    { 262L, null, null, null, null, 12, 4 },
                    { 263L, null, null, null, null, 13, 4 },
                    { 264L, null, null, null, null, 14, 4 },
                    { 265L, null, null, null, null, 15, 4 },
                    { 266L, null, null, null, null, 16, 4 },
                    { 267L, null, null, null, null, 17, 4 },
                    { 268L, null, null, null, null, 18, 4 },
                    { 269L, null, null, null, null, 19, 4 },
                    { 270L, null, null, null, null, 20, 4 },
                    { 271L, null, null, null, null, 21, 4 },
                    { 272L, null, null, null, null, 22, 4 },
                    { 273L, null, null, null, null, 23, 4 },
                    { 274L, null, null, null, null, 24, 4 },
                    { 275L, null, null, null, null, 25, 4 },
                    { 276L, null, null, null, null, 26, 4 },
                    { 277L, null, null, null, null, 27, 4 },
                    { 278L, null, null, null, null, 28, 4 },
                    { 279L, null, null, null, null, 29, 4 },
                    { 280L, null, null, null, null, 30, 4 },
                    { 281L, null, null, null, null, 41, 4 },
                    { 282L, null, null, null, null, 42, 4 },
                    { 283L, null, null, null, null, 43, 4 },
                    { 284L, null, null, null, null, 44, 4 },
                    { 285L, null, null, null, null, 45, 4 },
                    { 286L, null, null, null, null, 46, 4 },
                    { 287L, null, null, null, null, 47, 4 },
                    { 288L, null, null, null, null, 48, 4 },
                    { 289L, null, null, null, null, 49, 4 },
                    { 290L, null, null, null, null, 50, 4 },
                    { 291L, null, null, null, null, 1, 5 },
                    { 292L, null, null, null, null, 11, 5 },
                    { 293L, null, null, null, null, 21, 5 },
                    { 294L, null, null, null, null, 31, 5 },
                    { 295L, null, null, null, null, 41, 5 },
                    { 296L, null, null, null, null, 51, 5 },
                    { 297L, null, null, null, null, 61, 5 },
                    { 298L, null, null, null, null, 71, 5 },
                    { 299L, null, null, null, null, 81, 5 },
                    { 300L, null, null, null, null, 91, 5 },
                    { 301L, null, null, null, null, 101, 5 },
                    { 302L, null, null, null, null, 11, 3 },
                    { 303L, null, null, null, null, 31, 3 },
                    { 304L, null, null, null, null, 31, 4 }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Roles",
                columns: new[] { "RoleId", "CreatedAt", "CreatedBy", "CustomerId", "Description", "DisplayName", "IsActive", "IsSystemRole", "ModifiedAt", "ModifiedBy", "SystemName" },
                values: new object[,]
                {
                    { 1, null, null, null, null, "Owner", true, true, null, null, "Owner" },
                    { 2, null, null, null, null, "Administrator", true, true, null, null, "Administrator" },
                    { 3, null, null, null, null, "Accountant", true, true, null, null, "Accountant" },
                    { 4, null, null, null, null, "Sales", true, true, null, null, "Sales" },
                    { 5, null, null, null, null, "Viewer", true, true, null, null, "Viewer" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "TransactionTypes",
                columns: new[] { "Code", "CreatedAt", "CreatedBy", "IsActive", "IsLedgerPosting", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[,]
                {
                    { "BIL", null, null, true, true, null, null, "Bill" },
                    { "CRN", null, null, true, true, null, null, "Credit Note" },
                    { "DBN", null, null, true, true, null, null, "Debit Note" },
                    { "DEP", null, null, true, true, null, null, "Depreciation" },
                    { "DLC", null, null, true, true, null, null, "Delivery Challan" },
                    { "GRN", null, null, true, true, null, null, "Goods Receipt" },
                    { "INV", null, null, true, true, null, null, "Invoice" },
                    { "JRN", null, null, true, true, null, null, "Journal" },
                    { "OPB", null, null, true, true, null, null, "Opening Balance" },
                    { "POR", null, null, true, false, null, null, "Purchase Order" },
                    { "POS", null, null, true, true, null, null, "POS Sale" },
                    { "QTE", null, null, true, false, null, null, "Quote" },
                    { "RCM", null, null, true, true, null, null, "Receive Money" },
                    { "SOR", null, null, true, false, null, null, "Sales Order" },
                    { "SPM", null, null, true, true, null, null, "Spend Money" },
                    { "STA", null, null, true, true, null, null, "Stock Adjustment" },
                    { "TRM", null, null, true, true, null, null, "Transfer Money" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "SubMenus",
                columns: new[] { "SubMenuId", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "MenuId", "ModifiedAt", "ModifiedBy", "Name", "RoutePath" },
                values: new object[,]
                {
                    { 1, "dashboard", null, null, 1, "dashboard", true, 1, null, null, "Dashboard", "/dashboard" },
                    { 2, "sales_invoice", null, null, 1, "receipt", true, 2, null, null, "Invoice", "/sales/invoices" },
                    { 3, "sales_order", null, null, 2, "assignment", true, 2, null, null, "Sales Order", "/sales/orders" },
                    { 4, "sales_quote", null, null, 3, "description", true, 2, null, null, "Quote", "/sales/quotes" },
                    { 5, "sales_delivery_challan", null, null, 4, "local_shipping", true, 2, null, null, "Delivery Challan", "/sales/delivery-challans" },
                    { 6, "sales_credit_note", null, null, 5, "credit_card", true, 2, null, null, "Credit Note", "/sales/credit-notes" },
                    { 7, "sales_pos", null, null, 6, "point_of_sale", true, 2, null, null, "POS Sale", "/sales/pos" },
                    { 8, "purchase_bill", null, null, 1, "receipt_long", true, 3, null, null, "Bill", "/purchase/bills" },
                    { 9, "purchase_order", null, null, 2, "shopping_cart", true, 3, null, null, "Purchase Order", "/purchase/orders" },
                    { 10, "purchase_goods_receipt", null, null, 3, "inventory_2", true, 3, null, null, "Goods Receipt", "/purchase/goods-receipts" },
                    { 11, "purchase_debit_note", null, null, 4, "note", true, 3, null, null, "Debit Note", "/purchase/debit-notes" },
                    { 12, "inventory_items", null, null, 1, "inventory", true, 4, null, null, "Items", "/inventory/items" },
                    { 13, "inventory_categories", null, null, 2, "category", true, 4, null, null, "Categories", "/inventory/categories" },
                    { 14, "inventory_stock", null, null, 3, "storage", true, 4, null, null, "Stock", "/inventory/stock" },
                    { 15, "inventory_warehouses", null, null, 4, "warehouse", true, 4, null, null, "Warehouses", "/inventory/warehouses" },
                    { 16, "inventory_uom", null, null, 5, "straighten", true, 4, null, null, "Units of Measure", "/inventory/uom" },
                    { 17, "inventory_stock_adjustment", null, null, 6, "tune", true, 4, null, null, "Stock Adjustment", "/inventory/stock-adjustments" },
                    { 18, "accounting_chart", null, null, 1, "account_tree", true, 5, null, null, "Chart of Accounts", "/accounting/chart-of-accounts" },
                    { 19, "accounting_journal", null, null, 2, "edit_note", true, 5, null, null, "Journal Entries", "/accounting/journals" },
                    { 20, "accounting_ledger", null, null, 3, "ledger", true, 5, null, null, "Account Ledger", "/accounting/ledger" },
                    { 21, "accounting_trial_balance", null, null, 4, "balance", true, 5, null, null, "Trial Balance", "/accounting/trial-balance" },
                    { 22, "accounting_opening_balance", null, null, 5, "open_in_new", true, 5, null, null, "Opening Balance", "/accounting/opening-balance" },
                    { 23, "accounting_period_lock", null, null, 6, "lock", true, 5, null, null, "Period Locks", "/accounting/period-locks" },
                    { 24, "accounting_tax", null, null, 7, "receipt", true, 5, null, null, "Tax Master", "/accounting/tax-master" },
                    { 25, "accounting_payment_terms", null, null, 8, "schedule", true, 5, null, null, "Payment Terms", "/accounting/payment-terms" },
                    { 26, "accounting_numbering", null, null, 9, "format_list_numbered", true, 5, null, null, "Numbering Series", "/accounting/numbering-series" },
                    { 27, "banking_banks", null, null, 1, "business", true, 6, null, null, "Banks", "/banking/banks" },
                    { 28, "banking_accounts", null, null, 2, "account_balance", true, 6, null, null, "Bank Accounts", "/banking/accounts" },
                    { 29, "banking_spend_money", null, null, 3, "money_off", true, 6, null, null, "Spend Money", "/banking/spend-money" },
                    { 30, "banking_receive_money", null, null, 4, "monetization_on", true, 6, null, null, "Receive Money", "/banking/receive-money" },
                    { 31, "banking_transfer_money", null, null, 5, "swap_horiz", true, 6, null, null, "Transfer Money", "/banking/transfer-money" },
                    { 32, "banking_statements", null, null, 6, "description", true, 6, null, null, "Bank Statements", "/banking/statements" },
                    { 33, "contacts_list", null, null, 1, "person", true, 7, null, null, "Contacts", "/contacts" },
                    { 34, "reports_list", null, null, 1, "list", true, 8, null, null, "Reports", "/reports" },
                    { 35, "settings_organization", null, null, 1, "business", true, 9, null, null, "Organization", "/settings/organization" },
                    { 36, "settings_currencies", null, null, 2, "currency_exchange", true, 9, null, null, "Currencies", "/settings/currencies" },
                    { 37, "settings_smtp", null, null, 3, "email", true, 9, null, null, "Email Settings", "/settings/email" },
                    { 38, "settings_users", null, null, 4, "person_add", true, 9, null, null, "Users", "/settings/users" },
                    { 39, "settings_roles", null, null, 5, "security", true, 9, null, null, "Roles", "/settings/roles" },
                    { 40, "settings_branches", null, null, 6, "location_city", true, 9, null, null, "Branches", "/settings/branches" },
                    { 41, "settings_config", null, null, 7, "tune", true, 9, null, null, "Configuration", "/settings/configuration" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "SubMenuPermissions",
                columns: new[] { "SubMenuPermissionId", "Action", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode", "SubMenuId" },
                values: new object[,]
                {
                    { 1, "view", null, null, null, null, "dashboard", "dashboard.view", 1 },
                    { 2, "view", null, null, null, null, "sales", "sales.view", 2 },
                    { 3, "create", null, null, null, null, "sales", "sales.create", 2 },
                    { 4, "edit", null, null, null, null, "sales", "sales.edit", 2 },
                    { 5, "delete", null, null, null, null, "sales", "sales.delete", 2 },
                    { 6, "approve", null, null, null, null, "sales", "sales.approve", 2 },
                    { 7, "void", null, null, null, null, "sales", "sales.void", 2 },
                    { 8, "print", null, null, null, null, "sales", "sales.print", 2 },
                    { 9, "export", null, null, null, null, "sales", "sales.export", 2 },
                    { 10, "import", null, null, null, null, "sales", "sales.import", 2 },
                    { 11, "AllUserData", null, null, null, null, "sales", "sales.AllUserData", 2 },
                    { 12, "view", null, null, null, null, "sales", "sales.view", 3 },
                    { 13, "create", null, null, null, null, "sales", "sales.create", 3 },
                    { 14, "edit", null, null, null, null, "sales", "sales.edit", 3 },
                    { 15, "approve", null, null, null, null, "sales", "sales.approve", 3 },
                    { 16, "void", null, null, null, null, "sales", "sales.void", 3 },
                    { 17, "print", null, null, null, null, "sales", "sales.print", 3 },
                    { 18, "export", null, null, null, null, "sales", "sales.export", 3 },
                    { 19, "view", null, null, null, null, "sales", "sales.view", 4 },
                    { 20, "create", null, null, null, null, "sales", "sales.create", 4 },
                    { 21, "edit", null, null, null, null, "sales", "sales.edit", 4 },
                    { 22, "approve", null, null, null, null, "sales", "sales.approve", 4 },
                    { 23, "void", null, null, null, null, "sales", "sales.void", 4 },
                    { 24, "print", null, null, null, null, "sales", "sales.print", 4 },
                    { 25, "export", null, null, null, null, "sales", "sales.export", 4 },
                    { 26, "view", null, null, null, null, "sales", "sales.view", 5 },
                    { 27, "create", null, null, null, null, "sales", "sales.create", 5 },
                    { 28, "edit", null, null, null, null, "sales", "sales.edit", 5 },
                    { 29, "void", null, null, null, null, "sales", "sales.void", 5 },
                    { 30, "print", null, null, null, null, "sales", "sales.print", 5 },
                    { 31, "export", null, null, null, null, "sales", "sales.export", 5 },
                    { 32, "view", null, null, null, null, "sales", "sales.view", 6 },
                    { 33, "create", null, null, null, null, "sales", "sales.create", 6 },
                    { 34, "edit", null, null, null, null, "sales", "sales.edit", 6 },
                    { 35, "void", null, null, null, null, "sales", "sales.void", 6 },
                    { 36, "print", null, null, null, null, "sales", "sales.print", 6 },
                    { 37, "export", null, null, null, null, "sales", "sales.export", 6 },
                    { 38, "view", null, null, null, null, "sales", "sales.view", 7 },
                    { 39, "create", null, null, null, null, "sales", "sales.create", 7 },
                    { 40, "print", null, null, null, null, "sales", "sales.print", 7 },
                    { 41, "view", null, null, null, null, "purchase", "purchase.view", 8 },
                    { 42, "create", null, null, null, null, "purchase", "purchase.create", 8 },
                    { 43, "edit", null, null, null, null, "purchase", "purchase.edit", 8 },
                    { 44, "delete", null, null, null, null, "purchase", "purchase.delete", 8 },
                    { 45, "approve", null, null, null, null, "purchase", "purchase.approve", 8 },
                    { 46, "void", null, null, null, null, "purchase", "purchase.void", 8 },
                    { 47, "print", null, null, null, null, "purchase", "purchase.print", 8 },
                    { 48, "export", null, null, null, null, "purchase", "purchase.export", 8 },
                    { 49, "import", null, null, null, null, "purchase", "purchase.import", 8 },
                    { 50, "AllUserData", null, null, null, null, "purchase", "purchase.AllUserData", 8 },
                    { 51, "view", null, null, null, null, "purchase", "purchase.view", 9 },
                    { 52, "create", null, null, null, null, "purchase", "purchase.create", 9 },
                    { 53, "edit", null, null, null, null, "purchase", "purchase.edit", 9 },
                    { 54, "approve", null, null, null, null, "purchase", "purchase.approve", 9 },
                    { 55, "void", null, null, null, null, "purchase", "purchase.void", 9 },
                    { 56, "print", null, null, null, null, "purchase", "purchase.print", 9 },
                    { 57, "export", null, null, null, null, "purchase", "purchase.export", 9 },
                    { 58, "view", null, null, null, null, "purchase", "purchase.view", 10 },
                    { 59, "create", null, null, null, null, "purchase", "purchase.create", 10 },
                    { 60, "edit", null, null, null, null, "purchase", "purchase.edit", 10 },
                    { 61, "void", null, null, null, null, "purchase", "purchase.void", 10 },
                    { 62, "print", null, null, null, null, "purchase", "purchase.print", 10 },
                    { 63, "export", null, null, null, null, "purchase", "purchase.export", 10 },
                    { 64, "view", null, null, null, null, "purchase", "purchase.view", 11 },
                    { 65, "create", null, null, null, null, "purchase", "purchase.create", 11 },
                    { 66, "edit", null, null, null, null, "purchase", "purchase.edit", 11 },
                    { 67, "void", null, null, null, null, "purchase", "purchase.void", 11 },
                    { 68, "print", null, null, null, null, "purchase", "purchase.print", 11 },
                    { 69, "export", null, null, null, null, "purchase", "purchase.export", 11 },
                    { 70, "view", null, null, null, null, "inventory", "inventory.view", 12 },
                    { 71, "create", null, null, null, null, "inventory", "inventory.create", 12 },
                    { 72, "edit", null, null, null, null, "inventory", "inventory.edit", 12 },
                    { 73, "delete", null, null, null, null, "inventory", "inventory.delete", 12 },
                    { 74, "print", null, null, null, null, "inventory", "inventory.print", 12 },
                    { 75, "export", null, null, null, null, "inventory", "inventory.export", 12 },
                    { 76, "import", null, null, null, null, "inventory", "inventory.import", 12 },
                    { 77, "AllUserData", null, null, null, null, "inventory", "inventory.AllUserData", 12 },
                    { 78, "view", null, null, null, null, "inventory", "inventory.view", 13 },
                    { 79, "create", null, null, null, null, "inventory", "inventory.create", 13 },
                    { 80, "edit", null, null, null, null, "inventory", "inventory.edit", 13 },
                    { 81, "delete", null, null, null, null, "inventory", "inventory.delete", 13 },
                    { 82, "view", null, null, null, null, "inventory", "inventory.view", 14 },
                    { 83, "export", null, null, null, null, "inventory", "inventory.export", 14 },
                    { 84, "view", null, null, null, null, "inventory", "inventory.view", 15 },
                    { 85, "create", null, null, null, null, "inventory", "inventory.create", 15 },
                    { 86, "edit", null, null, null, null, "inventory", "inventory.edit", 15 },
                    { 87, "view", null, null, null, null, "inventory", "inventory.view", 16 },
                    { 88, "create", null, null, null, null, "inventory", "inventory.create", 16 },
                    { 89, "edit", null, null, null, null, "inventory", "inventory.edit", 16 },
                    { 90, "view", null, null, null, null, "inventory", "inventory.view", 17 },
                    { 91, "create", null, null, null, null, "inventory", "inventory.create", 17 },
                    { 92, "void", null, null, null, null, "inventory", "inventory.void", 17 },
                    { 93, "view", null, null, null, null, "accounting", "accounting.view", 18 },
                    { 94, "create", null, null, null, null, "accounting", "accounting.create", 18 },
                    { 95, "edit", null, null, null, null, "accounting", "accounting.edit", 18 },
                    { 96, "delete", null, null, null, null, "accounting", "accounting.delete", 18 },
                    { 97, "print", null, null, null, null, "accounting", "accounting.print", 18 },
                    { 98, "export", null, null, null, null, "accounting", "accounting.export", 18 },
                    { 99, "AllUserData", null, null, null, null, "accounting", "accounting.AllUserData", 18 },
                    { 100, "view", null, null, null, null, "accounting", "accounting.view", 19 },
                    { 101, "create", null, null, null, null, "accounting", "accounting.create", 19 },
                    { 102, "edit", null, null, null, null, "accounting", "accounting.edit", 19 },
                    { 103, "approve", null, null, null, null, "accounting", "accounting.approve", 19 },
                    { 104, "void", null, null, null, null, "accounting", "accounting.void", 19 },
                    { 105, "print", null, null, null, null, "accounting", "accounting.print", 19 },
                    { 106, "export", null, null, null, null, "accounting", "accounting.export", 19 },
                    { 107, "view", null, null, null, null, "accounting", "accounting.view", 20 },
                    { 108, "export", null, null, null, null, "accounting", "accounting.export", 20 },
                    { 109, "view", null, null, null, null, "accounting", "accounting.view", 21 },
                    { 110, "export", null, null, null, null, "accounting", "accounting.export", 21 },
                    { 111, "view", null, null, null, null, "accounting", "accounting.view", 22 },
                    { 112, "create", null, null, null, null, "accounting", "accounting.create", 22 },
                    { 113, "edit", null, null, null, null, "accounting", "accounting.edit", 22 },
                    { 114, "void", null, null, null, null, "accounting", "accounting.void", 22 },
                    { 115, "view", null, null, null, null, "accounting", "accounting.view", 23 },
                    { 116, "edit", null, null, null, null, "accounting", "accounting.edit", 23 },
                    { 117, "view", null, null, null, null, "accounting", "accounting.view", 24 },
                    { 118, "create", null, null, null, null, "accounting", "accounting.create", 24 },
                    { 119, "edit", null, null, null, null, "accounting", "accounting.edit", 24 },
                    { 120, "export", null, null, null, null, "accounting", "accounting.export", 24 },
                    { 121, "view", null, null, null, null, "accounting", "accounting.view", 25 },
                    { 122, "create", null, null, null, null, "accounting", "accounting.create", 25 },
                    { 123, "edit", null, null, null, null, "accounting", "accounting.edit", 25 },
                    { 124, "delete", null, null, null, null, "accounting", "accounting.delete", 25 },
                    { 125, "view", null, null, null, null, "accounting", "accounting.view", 26 },
                    { 126, "create", null, null, null, null, "accounting", "accounting.create", 26 },
                    { 127, "edit", null, null, null, null, "accounting", "accounting.edit", 26 },
                    { 128, "delete", null, null, null, null, "accounting", "accounting.delete", 26 },
                    { 129, "view", null, null, null, null, "banking", "banking.view", 27 },
                    { 130, "create", null, null, null, null, "banking", "banking.create", 27 },
                    { 131, "edit", null, null, null, null, "banking", "banking.edit", 27 },
                    { 132, "delete", null, null, null, null, "banking", "banking.delete", 27 },
                    { 133, "view", null, null, null, null, "banking", "banking.view", 28 },
                    { 134, "create", null, null, null, null, "banking", "banking.create", 28 },
                    { 135, "edit", null, null, null, null, "banking", "banking.edit", 28 },
                    { 136, "delete", null, null, null, null, "banking", "banking.delete", 28 },
                    { 137, "view", null, null, null, null, "banking", "banking.view", 29 },
                    { 138, "create", null, null, null, null, "banking", "banking.create", 29 },
                    { 139, "edit", null, null, null, null, "banking", "banking.edit", 29 },
                    { 140, "void", null, null, null, null, "banking", "banking.void", 29 },
                    { 141, "print", null, null, null, null, "banking", "banking.print", 29 },
                    { 142, "export", null, null, null, null, "banking", "banking.export", 29 },
                    { 143, "view", null, null, null, null, "banking", "banking.view", 30 },
                    { 144, "create", null, null, null, null, "banking", "banking.create", 30 },
                    { 145, "edit", null, null, null, null, "banking", "banking.edit", 30 },
                    { 146, "void", null, null, null, null, "banking", "banking.void", 30 },
                    { 147, "print", null, null, null, null, "banking", "banking.print", 30 },
                    { 148, "export", null, null, null, null, "banking", "banking.export", 30 },
                    { 149, "view", null, null, null, null, "banking", "banking.view", 31 },
                    { 150, "create", null, null, null, null, "banking", "banking.create", 31 },
                    { 151, "void", null, null, null, null, "banking", "banking.void", 31 },
                    { 152, "view", null, null, null, null, "banking", "banking.view", 32 },
                    { 153, "create", null, null, null, null, "banking", "banking.create", 32 },
                    { 154, "edit", null, null, null, null, "banking", "banking.edit", 32 },
                    { 155, "print", null, null, null, null, "banking", "banking.print", 32 },
                    { 156, "export", null, null, null, null, "banking", "banking.export", 32 },
                    { 157, "view", null, null, null, null, "contacts", "contacts.view", 33 },
                    { 158, "create", null, null, null, null, "contacts", "contacts.create", 33 },
                    { 159, "edit", null, null, null, null, "contacts", "contacts.edit", 33 },
                    { 160, "delete", null, null, null, null, "contacts", "contacts.delete", 33 },
                    { 161, "export", null, null, null, null, "contacts", "contacts.export", 33 },
                    { 162, "AllUserData", null, null, null, null, "contacts", "contacts.AllUserData", 33 },
                    { 163, "view", null, null, null, null, "reports", "reports.view", 34 },
                    { 164, "export", null, null, null, null, "reports", "reports.export", 34 },
                    { 165, "AllUserData", null, null, null, null, "reports", "reports.AllUserData", 34 },
                    { 166, "view", null, null, null, null, "settings", "settings.view", 35 },
                    { 167, "edit", null, null, null, null, "settings", "settings.edit", 35 },
                    { 168, "view", null, null, null, null, "settings", "settings.view", 36 },
                    { 169, "edit", null, null, null, null, "settings", "settings.edit", 36 },
                    { 170, "view", null, null, null, null, "settings", "settings.view", 37 },
                    { 171, "edit", null, null, null, null, "settings", "settings.edit", 37 },
                    { 172, "view", null, null, null, null, "settings", "settings.view", 38 },
                    { 173, "create", null, null, null, null, "settings", "settings.create", 38 },
                    { 174, "edit", null, null, null, null, "settings", "settings.edit", 38 },
                    { 175, "delete", null, null, null, null, "settings", "settings.delete", 38 },
                    { 176, "view", null, null, null, null, "settings", "settings.view", 39 },
                    { 177, "create", null, null, null, null, "settings", "settings.create", 39 },
                    { 178, "edit", null, null, null, null, "settings", "settings.edit", 39 },
                    { 179, "delete", null, null, null, null, "settings", "settings.delete", 39 },
                    { 180, "view", null, null, null, null, "settings", "settings.view", 40 },
                    { 181, "create", null, null, null, null, "settings", "settings.create", 40 },
                    { 182, "edit", null, null, null, null, "settings", "settings.edit", 40 },
                    { 183, "view", null, null, null, null, "settings", "settings.view", 41 },
                    { 184, "edit", null, null, null, null, "settings", "settings.edit", 41 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_SystemName",
                schema: "mst",
                table: "AccountTypes",
                column: "SystemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_Code",
                schema: "mst",
                table: "Configurations",
                column: "Code",
                unique: true,
                filter: "\"OrgId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_OrgId_Code",
                schema: "mst",
                table: "Configurations",
                columns: new[] { "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_CountryCode",
                schema: "mst",
                table: "Countries",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                schema: "mst",
                table: "Currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerCode",
                schema: "mst",
                table: "Customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsnSacCodes_Code",
                schema: "mst",
                table: "HsnSacCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsnSacCodes_CodeType_ChapterCode",
                schema: "mst",
                table: "HsnSacCodes",
                columns: new[] { "CodeType", "ChapterCode" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerSources_Code",
                schema: "mst",
                table: "LedgerSources",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTypes_Code",
                schema: "mst",
                table: "LedgerTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_CustomerId",
                schema: "mst",
                table: "Licenses",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistories_UserId_LoginAt",
                schema: "mst",
                table: "LoginHistories",
                columns: new[] { "UserId", "LoginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Menus_Code",
                schema: "mst",
                table: "Menus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgCurrencies_OrgId",
                schema: "mst",
                table: "OrgCurrencies",
                column: "OrgId",
                unique: true,
                filter: "\"IsBaseCurrency\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_OrgCurrencies_OrgId_CurrencyId",
                schema: "mst",
                table: "OrgCurrencies",
                columns: new[] { "OrgId", "CurrencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_CustomerId",
                schema: "mst",
                table: "Organizations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_CustomerId_Name",
                schema: "mst",
                table: "Organizations",
                columns: new[] { "CustomerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_CustomerId_OrgCode",
                schema: "mst",
                table: "Organizations",
                columns: new[] { "CustomerId", "OrgCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ExpiryDate",
                schema: "mst",
                table: "Organizations",
                column: "ExpiryDate",
                filter: "\"ExpiryDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OtpVerifications_UserId_Purpose_ExpiresAt",
                schema: "mst",
                table: "OtpVerifications",
                columns: new[] { "UserId", "Purpose", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                schema: "mst",
                table: "PasswordResetTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "mst",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "mst",
                table: "RefreshTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAt",
                schema: "mst",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CustomerId_SystemName",
                schema: "mst",
                table: "Roles",
                columns: new[] { "CustomerId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SystemName",
                schema: "mst",
                table: "Roles",
                column: "SystemName",
                unique: true,
                filter: "\"CustomerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SmtpSettings_CustomerId",
                schema: "mst",
                table: "SmtpSettings",
                column: "CustomerId",
                unique: true,
                filter: "\"CustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_States_CountryId_StateCode",
                schema: "mst",
                table: "States",
                columns: new[] { "CountryId", "StateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubMenuPermissions_SubMenuId_PermissionCode",
                schema: "mst",
                table: "SubMenuPermissions",
                columns: new[] { "SubMenuId", "PermissionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubMenus_MenuId_Code",
                schema: "mst",
                table: "SubMenus",
                columns: new[] { "MenuId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_Name",
                schema: "mst",
                table: "TransactionTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationRoles_OrgId",
                schema: "mst",
                table: "UserOrganizationRoles",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationRoles_UserId",
                schema: "mst",
                table: "UserOrganizationRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationRoles_UserId_OrgId_RoleId",
                schema: "mst",
                table: "UserOrganizationRoles",
                columns: new[] { "UserId", "OrgId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "mst",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTypes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Configurations",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "HsnSacCodes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "LedgerSources",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "LedgerTypes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Licenses",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "LoginHistories",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "OrgCurrencies",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "OtpVerifications",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "SmtpSettings",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "States",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "SubMenuPermissions",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "TransactionTypes",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "UserOrganizationRoles",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Countries",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "SubMenus",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "Menus",
                schema: "mst");
        }
    }
}
