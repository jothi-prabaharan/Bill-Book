using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_01_InitialSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cus");

            migrationBuilder.CreateTable(
                name: "Audiences",
                schema: "cus",
                columns: table => new
                {
                    AudienceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rules = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_Audiences", x => x.AudienceId);
                });

            migrationBuilder.CreateTable(
                name: "AutomationEnrolments",
                schema: "cus",
                columns: table => new
                {
                    AutomationEnrolmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AutomationId = table.Column<long>(type: "bigint", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExitReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AutomationEnrolments", x => x.AutomationEnrolmentId);
                });

            migrationBuilder.CreateTable(
                name: "Automations",
                schema: "cus",
                columns: table => new
                {
                    AutomationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TriggerSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TriggerStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_Automations", x => x.AutomationId);
                });

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

            migrationBuilder.CreateTable(
                name: "EditLog",
                schema: "cus",
                columns: table => new
                {
                    EditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RootEntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RootEntityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Changes = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_EditLog", x => x.EditLogId);
                });

            migrationBuilder.CreateTable(
                name: "EmailConsents",
                schema: "cus",
                columns: table => new
                {
                    EmailConsentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_EmailConsents", x => x.EmailConsentId);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                schema: "cus",
                columns: table => new
                {
                    EmailTemplateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_EmailTemplates", x => x.EmailTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "cus",
                columns: table => new
                {
                    ErrorLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ErrorReference = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    SqlState = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    JobReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExceptionType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Hint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Where = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Routine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConstraintName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SchemaName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ColumnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StackTrace = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    InnerExceptions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FollowUpStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FollowUpNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ErrorLogs", x => x.ErrorLogId);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                schema: "cus",
                columns: table => new
                {
                    LeadId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConvertedContactId = table.Column<long>(type: "bigint", nullable: true),
                    ConvertedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_Leads", x => x.LeadId);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyAccounts",
                schema: "cus",
                columns: table => new
                {
                    LoyaltyAccountId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    PointsBalance = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LoyaltyAccounts", x => x.LoyaltyAccountId);
                });

            migrationBuilder.CreateTable(
                name: "ServicePlans",
                schema: "cus",
                columns: table => new
                {
                    ServicePlanId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    AssetId = table.Column<long>(type: "bigint", nullable: true),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    Every = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeadDays = table.Column<int>(type: "integer", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastServicedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LastTicketId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ServicePlans", x => x.ServicePlanId);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                schema: "cus",
                columns: table => new
                {
                    SlaPolicyId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResponseHours = table.Column<int>(type: "integer", nullable: false),
                    ResolutionHours = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SlaPolicies", x => x.SlaPolicyId);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "cus",
                columns: table => new
                {
                    TicketId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SlaDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_Tickets", x => x.TicketId);
                });

            migrationBuilder.CreateTable(
                name: "AutomationSteps",
                schema: "cus",
                columns: table => new
                {
                    AutomationStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AutomationId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WaitDays = table.Column<int>(type: "integer", nullable: false),
                    EmailTemplateId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_AutomationSteps", x => x.AutomationStepId);
                    table.ForeignKey(
                        name: "FK_AutomationSteps_Automations_AutomationId",
                        column: x => x.AutomationId,
                        principalSchema: "cus",
                        principalTable: "Automations",
                        principalColumn: "AutomationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                schema: "cus",
                columns: table => new
                {
                    LoyaltyTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoyaltyAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.LoyaltyTransactionId);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_LoyaltyAccounts_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalSchema: "cus",
                        principalTable: "LoyaltyAccounts",
                        principalColumn: "LoyaltyAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                schema: "cus",
                columns: table => new
                {
                    TicketMessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TicketMessages", x => x.TicketMessageId);
                    table.CheckConstraint("chk_ticketmessages_internal_by_staff", "NOT \"IsInternal\" OR \"AuthorType\" = 'User'");
                    table.ForeignKey(
                        name: "FK_TicketMessages_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "cus",
                        principalTable: "Tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_CustomerId_OrgId",
                schema: "cus",
                table: "Audiences",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_OrgId_IsActive",
                schema: "cus",
                table: "Audiences",
                columns: new[] { "OrgId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationEnrolments_CustomerId_OrgId",
                schema: "cus",
                table: "AutomationEnrolments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationEnrolments_OrgId_AutomationId_Email_Status",
                schema: "cus",
                table: "AutomationEnrolments",
                columns: new[] { "OrgId", "AutomationId", "Email", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationEnrolments_OrgId_Status_NextRunAt",
                schema: "cus",
                table: "AutomationEnrolments",
                columns: new[] { "OrgId", "Status", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_AutomationId",
                schema: "cus",
                table: "AutomationSteps",
                column: "AutomationId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_CustomerId_OrgId",
                schema: "cus",
                table: "AutomationSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_OrgId_AutomationId_Sequence",
                schema: "cus",
                table: "AutomationSteps",
                columns: new[] { "OrgId", "AutomationId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_CustomerId_OrgId",
                schema: "cus",
                table: "Automations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_OrgId_IsActive_TriggerType",
                schema: "cus",
                table: "Automations",
                columns: new[] { "OrgId", "IsActive", "TriggerType" });

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

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_CustomerId_OrgId",
                schema: "cus",
                table: "EditLog",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_ChangedAt",
                schema: "cus",
                table: "EditLog",
                columns: new[] { "OrgId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_EntityName_ChangedAt",
                schema: "cus",
                table: "EditLog",
                columns: new[] { "OrgId", "EntityName", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_RootEntityName_RootEntityKey_ChangedAt",
                schema: "cus",
                table: "EditLog",
                columns: new[] { "OrgId", "RootEntityName", "RootEntityKey", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EditLog_OrgId_UserId_ChangedAt",
                schema: "cus",
                table: "EditLog",
                columns: new[] { "OrgId", "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConsents_CustomerId_OrgId",
                schema: "cus",
                table: "EmailConsents",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConsents_OrgId_Email",
                schema: "cus",
                table: "EmailConsents",
                columns: new[] { "OrgId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_CustomerId_OrgId",
                schema: "cus",
                table: "EmailTemplates",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_OrgId_IsActive",
                schema: "cus",
                table: "EmailTemplates",
                columns: new[] { "OrgId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "cus",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "cus",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "cus",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CustomerId_OrgId",
                schema: "cus",
                table: "Leads",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_OrgId_Status",
                schema: "cus",
                table: "Leads",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAccounts_CustomerId_OrgId",
                schema: "cus",
                table: "LoyaltyAccounts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAccounts_OrgId_ContactId",
                schema: "cus",
                table: "LoyaltyAccounts",
                columns: new[] { "OrgId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId_OrgId",
                schema: "cus",
                table: "LoyaltyTransactions",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_LoyaltyAccountId",
                schema: "cus",
                table: "LoyaltyTransactions",
                column: "LoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_OrgId_Kind_Source_SourceId",
                schema: "cus",
                table: "LoyaltyTransactions",
                columns: new[] { "OrgId", "Kind", "Source", "SourceId" },
                unique: true,
                filter: "\"SourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_OrgId_LoyaltyAccountId_OccurredAt",
                schema: "cus",
                table: "LoyaltyTransactions",
                columns: new[] { "OrgId", "LoyaltyAccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlans_CustomerId_OrgId",
                schema: "cus",
                table: "ServicePlans",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlans_OrgId_ContactId",
                schema: "cus",
                table: "ServicePlans",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlans_OrgId_Status_NextDueDate",
                schema: "cus",
                table: "ServicePlans",
                columns: new[] { "OrgId", "Status", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_CustomerId_OrgId",
                schema: "cus",
                table: "SlaPolicies",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_OrgId_Priority",
                schema: "cus",
                table: "SlaPolicies",
                columns: new[] { "OrgId", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_CustomerId_OrgId",
                schema: "cus",
                table: "TicketMessages",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_OrgId_TicketId",
                schema: "cus",
                table: "TicketMessages",
                columns: new[] { "OrgId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId",
                schema: "cus",
                table: "TicketMessages",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CustomerId_OrgId",
                schema: "cus",
                table: "Tickets",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrgId_ContactId",
                schema: "cus",
                table: "Tickets",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrgId_Status",
                schema: "cus",
                table: "Tickets",
                columns: new[] { "OrgId", "Status" });
            // ---- Hand-written: what EF Core cannot generate (TK-185) ----------
            //
            // Row-level security on every tenant table: enabled, FORCEd, and one
            // policy on CustomerId and OrgId in TK-71's form, through
            // TenantRlsMigration. FORCE is what makes it bind, because the
            // application connects as the tables' owner. The edit log's policy and
            // its append-only triggers come from EditLogMigration (TK-118).
            //
            // Carry this block forward by hand in any future squash. The model
            // knows nothing of policies, triggers or functions, so a regenerated
            // migration drops them without a word, which is how RLS was lost on
            // 14 September 2026. A tenant table added later needs its own
            // TenantRlsMigration.Enable call; RlsAudit fails until it has one.
            foreach ((string schema, string[] tables) in RlsTables)
            {
                foreach (string table in tables)
                {
                    Shared.Kernel.Persistence.TenantRlsMigration.Enable(migrationBuilder, schema, table);
                }
            }

            Shared.Kernel.Audit.EditLogMigration.Up(migrationBuilder, "cus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Shared.Kernel.Audit.EditLogMigration.Down(migrationBuilder, "cus");

            migrationBuilder.DropTable(
                name: "Audiences",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "AutomationEnrolments",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "AutomationSteps",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "CampaignLinks",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "CampaignRecipients",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "Campaigns",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "EditLog",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "EmailConsents",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "EmailTemplates",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "Leads",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "ServicePlans",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "SlaPolicies",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "TicketMessages",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "Automations",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "LoyaltyAccounts",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "cus");
        }

        /// <summary>
        /// Every table guarded by the standard tenant policy, written out rather
        /// than derived at run time: a migration must do the same thing forever.
        /// Taken from the policies the old chains left in a migrated database.
        /// </summary>
        private static readonly (string Schema, string[] Tables)[] RlsTables =
        [
            ("cus",
            [
                "Audiences",
                "AutomationEnrolments",
                "AutomationSteps",
                "Automations",
                "CampaignLinks",
                "CampaignRecipients",
                "Campaigns",
                "EmailConsents",
                "EmailTemplates",
                "ErrorLogs",
                "Leads",
                "LoyaltyAccounts",
                "LoyaltyTransactions",
                "ServicePlans",
                "SlaPolicies",
                "TicketMessages",
                "Tickets",
            ]),
        ];
    }
}
