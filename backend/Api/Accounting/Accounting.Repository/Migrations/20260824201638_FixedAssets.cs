using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FixedAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FixedAssetCategories",
                schema: "acc",
                columns: table => new
                {
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetAccountId = table.Column<long>(type: "bigint", nullable: false),
                    AccumulatedDepreciationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    DepreciationExpenseAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetCategories", x => x.FixedAssetCategoryId);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AccumulatedDepreciationAccoun~",
                        column: x => x.AccumulatedDepreciationAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AssetAccountId",
                        column: x => x.AssetAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_DepreciationExpenseAccountId",
                        column: x => x.DepreciationExpenseAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssets",
                schema: "acc",
                columns: table => new
                {
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FixedAssetCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PurchaseBillId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssets", x => x.FixedAssetId);
                    table.ForeignKey(
                        name: "FK_FixedAssets_FixedAssetCategories_FixedAssetCategoryId",
                        column: x => x.FixedAssetCategoryId,
                        principalSchema: "acc",
                        principalTable: "FixedAssetCategories",
                        principalColumn: "FixedAssetCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepreciationSchedules",
                schema: "acc",
                columns: table => new
                {
                    DepreciationScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    ScheduleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DepreciationMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    UsefulLifeYears = table.Column<int>(type: "integer", nullable: false),
                    DepreciationStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SalvageValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepreciationSchedules", x => x.DepreciationScheduleId);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalSchema: "acc",
                        principalTable: "FixedAssets",
                        principalColumn: "FixedAssetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTransactions",
                schema: "acc",
                columns: table => new
                {
                    AssetTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DepreciationScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    JournalId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTransactions", x => x.AssetTransactionId);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_DepreciationSchedules_DepreciationSchedul~",
                        column: x => x.DepreciationScheduleId,
                        principalSchema: "acc",
                        principalTable: "DepreciationSchedules",
                        principalColumn: "DepreciationScheduleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalSchema: "acc",
                        principalTable: "FixedAssets",
                        principalColumn: "FixedAssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTransactions_Journals_JournalId",
                        column: x => x.JournalId,
                        principalSchema: "acc",
                        principalTable: "Journals",
                        principalColumn: "JournalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_DepreciationScheduleId",
                schema: "acc",
                table: "AssetTransactions",
                column: "DepreciationScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_FixedAssetId",
                schema: "acc",
                table: "AssetTransactions",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_JournalId",
                schema: "acc",
                table: "AssetTransactions",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_OrgId",
                schema: "acc",
                table: "AssetTransactions",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_OrgId_FixedAssetId_TransactionDate",
                schema: "acc",
                table: "AssetTransactions",
                columns: new[] { "OrgId", "FixedAssetId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_FixedAssetId",
                schema: "acc",
                table: "DepreciationSchedules",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_OrgId",
                schema: "acc",
                table: "DepreciationSchedules",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_OrgId_FixedAssetId_ScheduleType",
                schema: "acc",
                table: "DepreciationSchedules",
                columns: new[] { "OrgId", "FixedAssetId", "ScheduleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AccumulatedDepreciationAccountId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "AccumulatedDepreciationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AssetAccountId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "AssetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_DepreciationExpenseAccountId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "DepreciationExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_OrgId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_OrgId_CategoryName",
                schema: "acc",
                table: "FixedAssetCategories",
                columns: new[] { "OrgId", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_FixedAssetCategoryId",
                schema: "acc",
                table: "FixedAssets",
                column: "FixedAssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_OrgId",
                schema: "acc",
                table: "FixedAssets",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_OrgId_AssetCode",
                schema: "acc",
                table: "FixedAssets",
                columns: new[] { "OrgId", "AssetCode" },
                unique: true);

            migrationBuilder.Sql(@"
                ALTER TABLE acc.""FixedAssetCategories"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantPolicy"" ON acc.""FixedAssetCategories"" AS PERMISSIVE FOR ALL TO public USING (""OrgId"" = current_setting('app.current_org_id', true)::uuid);
                
                ALTER TABLE acc.""FixedAssets"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantPolicy"" ON acc.""FixedAssets"" AS PERMISSIVE FOR ALL TO public USING (""OrgId"" = current_setting('app.current_org_id', true)::uuid);
                
                ALTER TABLE acc.""DepreciationSchedules"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantPolicy"" ON acc.""DepreciationSchedules"" AS PERMISSIVE FOR ALL TO public USING (""OrgId"" = current_setting('app.current_org_id', true)::uuid);
                
                ALTER TABLE acc.""AssetTransactions"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantPolicy"" ON acc.""AssetTransactions"" AS PERMISSIVE FOR ALL TO public USING (""OrgId"" = current_setting('app.current_org_id', true)::uuid);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetTransactions",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "DepreciationSchedules",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "FixedAssets",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "FixedAssetCategories",
                schema: "acc");
        }
    }
}
