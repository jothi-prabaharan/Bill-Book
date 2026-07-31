using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Identity.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "idn");

            migrationBuilder.CreateTable(
                name: "LoginHistories",
                schema: "idn",
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
                name: "OtpVerifications",
                schema: "idn",
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
                schema: "idn",
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
                schema: "idn",
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
                schema: "idn",
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
                schema: "idn",
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
                schema: "idn",
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
                name: "UserOrganizationRoles",
                schema: "idn",
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
                schema: "idn",
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

            migrationBuilder.InsertData(
                schema: "idn",
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
                schema: "idn",
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
                    { 301L, null, null, null, null, 101, 5 }
                });

            migrationBuilder.InsertData(
                schema: "idn",
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

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistories_UserId_LoginAt",
                schema: "idn",
                table: "LoginHistories",
                columns: new[] { "UserId", "LoginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpVerifications_UserId_Purpose_ExpiresAt",
                schema: "idn",
                table: "OtpVerifications",
                columns: new[] { "UserId", "Purpose", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                schema: "idn",
                table: "PasswordResetTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "idn",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "idn",
                table: "RefreshTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAt",
                schema: "idn",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                schema: "idn",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CustomerId_SystemName",
                schema: "idn",
                table: "Roles",
                columns: new[] { "CustomerId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SystemName",
                schema: "idn",
                table: "Roles",
                column: "SystemName",
                unique: true,
                filter: "\"CustomerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationRoles_OrgId",
                schema: "idn",
                table: "UserOrganizationRoles",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationRoles_UserId",
                schema: "idn",
                table: "UserOrganizationRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationRoles_UserId_OrgId_RoleId",
                schema: "idn",
                table: "UserOrganizationRoles",
                columns: new[] { "UserId", "OrgId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "idn",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginHistories",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "OtpVerifications",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "UserOrganizationRoles",
                schema: "idn");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "idn");
        }
    }
}
