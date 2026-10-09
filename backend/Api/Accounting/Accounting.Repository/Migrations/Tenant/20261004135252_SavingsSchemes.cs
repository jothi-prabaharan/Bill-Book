using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class SavingsSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavingsSchemes",
                schema: "acc",
                columns: table => new
                {
                    SavingsSchemeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstalmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: false),
                    BonusType = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    BonusValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GraceDays = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SavingsSchemes", x => x.SavingsSchemeId);
                });

            migrationBuilder.CreateTable(
                name: "SchemeEnrolments",
                schema: "acc",
                columns: table => new
                {
                    SchemeEnrolmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SavingsSchemeId = table.Column<long>(type: "bigint", nullable: false),
                    EnrolmentNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InstalmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: false),
                    BonusAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GraceDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PaidPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaturedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LapsedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RedeemedInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    RedeemedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PrincipalRedeemed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusRedeemed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusJournalId = table.Column<long>(type: "bigint", nullable: true),
                    RefundSpendMoneyId = table.Column<long>(type: "bigint", nullable: true),
                    RefundedOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_SchemeEnrolments", x => x.SchemeEnrolmentId);
                    table.ForeignKey(
                        name: "FK_SchemeEnrolments_SavingsSchemes_SavingsSchemeId",
                        column: x => x.SavingsSchemeId,
                        principalSchema: "acc",
                        principalTable: "SavingsSchemes",
                        principalColumn: "SavingsSchemeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchemeInstalments",
                schema: "acc",
                columns: table => new
                {
                    SchemeInstalmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchemeEnrolmentId = table.Column<long>(type: "bigint", nullable: false),
                    InstalmentNumber = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceiveMoneyId = table.Column<long>(type: "bigint", nullable: true),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_SchemeInstalments", x => x.SchemeInstalmentId);
                    table.ForeignKey(
                        name: "FK_SchemeInstalments_ReceiveMoney_ReceiveMoneyId",
                        column: x => x.ReceiveMoneyId,
                        principalSchema: "acc",
                        principalTable: "ReceiveMoney",
                        principalColumn: "ReceiveMoneyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchemeInstalments_SchemeEnrolments_SchemeEnrolmentId",
                        column: x => x.SchemeEnrolmentId,
                        principalSchema: "acc",
                        principalTable: "SchemeEnrolments",
                        principalColumn: "SchemeEnrolmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsSchemes_CustomerId_OrgId",
                schema: "acc",
                table: "SavingsSchemes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsSchemes_OrgId_Code",
                schema: "acc",
                table: "SavingsSchemes",
                columns: new[] { "OrgId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchemeEnrolments_CustomerId_OrgId",
                schema: "acc",
                table: "SchemeEnrolments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchemeEnrolments_EnrolmentNo",
                schema: "acc",
                table: "SchemeEnrolments",
                column: "EnrolmentNo",
                unique: true,
                filter: "\"EnrolmentNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SchemeEnrolments_OrgId_ContactId",
                schema: "acc",
                table: "SchemeEnrolments",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchemeEnrolments_OrgId_Status",
                schema: "acc",
                table: "SchemeEnrolments",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchemeEnrolments_SavingsSchemeId",
                schema: "acc",
                table: "SchemeEnrolments",
                column: "SavingsSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_SchemeInstalments_CustomerId_OrgId",
                schema: "acc",
                table: "SchemeInstalments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchemeInstalments_OrgId_SchemeEnrolmentId_InstalmentNumber",
                schema: "acc",
                table: "SchemeInstalments",
                columns: new[] { "OrgId", "SchemeEnrolmentId", "InstalmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchemeInstalments_ReceiveMoneyId",
                schema: "acc",
                table: "SchemeInstalments",
                column: "ReceiveMoneyId",
                unique: true,
                filter: "\"ReceiveMoneyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SchemeInstalments_SchemeEnrolmentId",
                schema: "acc",
                table: "SchemeInstalments",
                column: "SchemeEnrolmentId");

            // RLS in TK-71's form, after the tables exist (TK-141).
            TenantRlsMigration.Enable(migrationBuilder, "acc", "SavingsSchemes");
            TenantRlsMigration.Enable(migrationBuilder, "acc", "SchemeEnrolments");
            TenantRlsMigration.Enable(migrationBuilder, "acc", "SchemeInstalments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchemeInstalments",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "SchemeEnrolments",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "SavingsSchemes",
                schema: "acc");
        }
    }
}
