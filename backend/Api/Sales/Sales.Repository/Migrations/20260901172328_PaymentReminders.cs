using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class PaymentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderLogs",
                schema: "sal",
                columns: table => new
                {
                    ReminderLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    ReminderProfileId = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_ReminderLogs", x => x.ReminderLogId);
                });

            migrationBuilder.CreateTable(
                name: "ReminderProfiles",
                schema: "sal",
                columns: table => new
                {
                    ReminderProfileId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DaysOverdueTrigger = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ReminderProfiles", x => x.ReminderProfileId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderLogs_CustomerId_OrgId",
                schema: "sal",
                table: "ReminderLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderProfiles_CustomerId_OrgId",
                schema: "sal",
                table: "ReminderProfiles",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.Sql(@"
                ALTER TABLE sal.""ReminderLogs"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantPolicy"" ON sal.""ReminderLogs""
                    AS PERMISSIVE FOR ALL
                    TO public
                    USING (""OrgId"" = current_setting('app.current_org_id')::uuid AND ""CustomerId"" = current_setting('app.current_customer_id')::uuid);
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE sal.""ReminderProfiles"" ENABLE ROW LEVEL SECURITY;
                CREATE POLICY ""TenantPolicy"" ON sal.""ReminderProfiles""
                    AS PERMISSIVE FOR ALL
                    TO public
                    USING (""OrgId"" = current_setting('app.current_org_id')::uuid AND ""CustomerId"" = current_setting('app.current_customer_id')::uuid);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReminderLogs",
                schema: "sal");

            migrationBuilder.DropTable(
                name: "ReminderProfiles",
                schema: "sal");
        }
    }
}
