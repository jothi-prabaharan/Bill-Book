using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Worker.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotificationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No row-level security, and that is a named exemption rather than
            // an omission (TK-19). The table carries no CustomerId and no OrgId —
            // it holds message ids, a topic and two times, nothing of any
            // customer's — so there is no tenant for a policy to test, the same
            // reason rpt.ReportMasters and each schema's __EFMigrationsHistory
            // are exempt. A policy keyed on the sender's customer would also
            // refuse the platform's own mail, which has none.
            migrationBuilder.EnsureSchema(
                name: "ntf");

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                schema: "ntf",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ClaimedAt",
                schema: "ntf",
                table: "ProcessedMessages",
                column: "ClaimedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedMessages",
                schema: "ntf");
        }
    }
}
