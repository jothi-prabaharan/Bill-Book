using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RecurringInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "sal", "RecurringInvoiceSchedules");
            TenantRlsMigration.Enable(migrationBuilder, "sal", "RecurringInvoiceRuns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringInvoiceRuns",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "RecurringInvoiceSchedules",
                schema: "sal");
        }
    }
}
