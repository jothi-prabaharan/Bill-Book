using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddEInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UqcCode",
                schema: "sal",
                table: "InvoiceDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UqcCode",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UqcCode",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

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

            // Row-level security for both new tables, the TK-71 block as in
            // 20260924061419_EnableRowLevelSecurity.cs (TK-91).
            foreach (string table in new[] { "EInvoices", "EwayBills" })
            {
                string policy = table.ToLowerInvariant() + "_tenant_isolation";
                migrationBuilder.Sql($"""
                    ALTER TABLE sal."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE sal."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON sal."{table}";
                    CREATE POLICY {policy}
                        ON sal."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP POLICY IF EXISTS einvoices_tenant_isolation ON sal."EInvoices";""");
            migrationBuilder.Sql("""DROP POLICY IF EXISTS ewaybills_tenant_isolation ON sal."EwayBills";""");

            migrationBuilder.DropTable(
                name: "EInvoices",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "EwayBills",
                schema: "sal");

            migrationBuilder.DropColumn(
                name: "UqcCode",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "UqcCode",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropColumn(
                name: "UqcCode",
                schema: "sal",
                table: "CreditNoteDetails");
        }
    }
}
