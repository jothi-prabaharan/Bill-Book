using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCampaignsAndTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignLinks",
                schema: "cus",
                columns: table => new
                {
                    CampaignLinkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TrackingToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false),
                    FirstClickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastClickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_CampaignLinks", x => x.CampaignLinkId);
                });

            migrationBuilder.CreateTable(
                name: "CampaignRecipients",
                schema: "cus",
                columns: table => new
                {
                    CampaignRecipientId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<long>(type: "bigint", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    ContactPersonId = table.Column<long>(type: "bigint", nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UnsubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_CampaignRecipients", x => x.CampaignRecipientId);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                schema: "cus",
                columns: table => new
                {
                    CampaignId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmailTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HtmlBodySnapshot = table.Column<string>(type: "text", nullable: true),
                    TextBodySnapshot = table.Column<string>(type: "text", nullable: true),
                    AudienceId = table.Column<long>(type: "bigint", nullable: false),
                    FromName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReplyTo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false),
                    TotalSent = table.Column<int>(type: "integer", nullable: false),
                    TotalFailed = table.Column<int>(type: "integer", nullable: false),
                    TotalSuppressed = table.Column<int>(type: "integer", nullable: false),
                    TotalOpened = table.Column<int>(type: "integer", nullable: false),
                    TotalClicked = table.Column<int>(type: "integer", nullable: false),
                    TotalUnsubscribed = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Campaigns", x => x.CampaignId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLinks_CustomerId_OrgId",
                schema: "cus",
                table: "CampaignLinks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLinks_OrgId_CampaignId",
                schema: "cus",
                table: "CampaignLinks",
                columns: new[] { "OrgId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLinks_OrgId_TrackingToken",
                schema: "cus",
                table: "CampaignLinks",
                columns: new[] { "OrgId", "TrackingToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_CustomerId_OrgId",
                schema: "cus",
                table: "CampaignRecipients",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_OrgId_CampaignId_Email",
                schema: "cus",
                table: "CampaignRecipients",
                columns: new[] { "OrgId", "CampaignId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_OrgId_CampaignId_Status",
                schema: "cus",
                table: "CampaignRecipients",
                columns: new[] { "OrgId", "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CustomerId_OrgId",
                schema: "cus",
                table: "Campaigns",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_OrgId_ScheduledAt",
                schema: "cus",
                table: "Campaigns",
                columns: new[] { "OrgId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_OrgId_Status",
                schema: "cus",
                table: "Campaigns",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.Sql("""
                ALTER TABLE cus."Campaigns" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."Campaigns" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS campaigns_tenant_isolation ON cus."Campaigns";
                CREATE POLICY campaigns_tenant_isolation
                    ON cus."Campaigns"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);

                ALTER TABLE cus."CampaignRecipients" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."CampaignRecipients" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS campaignrecipients_tenant_isolation ON cus."CampaignRecipients";
                CREATE POLICY campaignrecipients_tenant_isolation
                    ON cus."CampaignRecipients"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);

                ALTER TABLE cus."CampaignLinks" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."CampaignLinks" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS campaignlinks_tenant_isolation ON cus."CampaignLinks";
                CREATE POLICY campaignlinks_tenant_isolation
                    ON cus."CampaignLinks"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS campaigns_tenant_isolation ON cus."Campaigns";
                DROP POLICY IF EXISTS campaignrecipients_tenant_isolation ON cus."CampaignRecipients";
                DROP POLICY IF EXISTS campaignlinks_tenant_isolation ON cus."CampaignLinks";
                """);

            migrationBuilder.DropTable(
                name: "CampaignLinks",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "CampaignRecipients",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "Campaigns",
                schema: "cus");
        }
    }
}
