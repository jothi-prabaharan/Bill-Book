using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "acc");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "acc",
                columns: table => new
                {
                    AccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountTypeId = table.Column<int>(type: "integer", nullable: false),
                    IsContra = table.Column<bool>(type: "boolean", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountSystemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentAccountId = table.Column<long>(type: "bigint", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    IsJE = table.Column<bool>(type: "boolean", nullable: false),
                    IsLock = table.Column<bool>(type: "boolean", nullable: false),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsPayment = table.Column<bool>(type: "boolean", nullable: false),
                    IsBank = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_Accounts_Accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxMasters",
                schema: "acc",
                columns: table => new
                {
                    TaxMasterId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxGroupId = table.Column<long>(type: "bigint", nullable: false),
                    TaxSystemName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CgstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    SgstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IgstRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CessRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsSales = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxMasters", x => x.TaxMasterId);
                    table.CheckConstraint("chk_tax_applicability", "\"IsSales\" = true OR \"IsPurchase\" = true");
                    table.CheckConstraint("chk_tax_effective_range", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.CheckConstraint("chk_tax_split", "\"CgstRate\" = \"SgstRate\" AND \"CgstRate\" + \"SgstRate\" = \"TotalRate\" AND \"IgstRate\" = \"TotalRate\"");
                });

            migrationBuilder.CreateTable(
                name: "SubAccounts",
                schema: "acc",
                columns: table => new
                {
                    SubAccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountTypeId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: false),
                    TaxComponent = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SubAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubAccounts", x => x.SubAccountId);
                    table.ForeignKey(
                        name: "FK_SubAccounts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrgId_AccountCode",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "OrgId", "AccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrgId_AccountTypeId",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "OrgId", "AccountTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ParentAccountId",
                schema: "acc",
                table: "Accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Purchase",
                schema: "acc",
                table: "Accounts",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_AccountId_ReferenceType_ReferenceId_TaxComponent",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "AccountId", "ReferenceType", "ReferenceId", "TaxComponent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_OrgId",
                schema: "acc",
                table: "SubAccounts",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_OrgId_ReferenceType_ReferenceId",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "OrgId", "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_OrgId_EffectiveFrom_EffectiveTo",
                schema: "acc",
                table: "TaxMasters",
                columns: new[] { "OrgId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_OrgId_TaxGroupId_EffectiveFrom",
                schema: "acc",
                table: "TaxMasters",
                columns: new[] { "OrgId", "TaxGroupId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_Purchase",
                schema: "acc",
                table: "TaxMasters",
                column: "OrgId",
                filter: "\"IsPurchase\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubAccounts",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "TaxMasters",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "acc");
        }
    }
}
