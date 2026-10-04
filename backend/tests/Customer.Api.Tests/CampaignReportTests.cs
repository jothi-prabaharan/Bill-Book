using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Customer.Repository.Clients;
using Customer.Repository.Repositories;
using Customer.Repository.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Customer;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Messaging;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class CampaignReportTests
{
    private readonly PostgresFixture _postgres;

    public CampaignReportTests(PostgresFixture postgres) => _postgres = postgres;

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Unsubscribe:SigningKey"] = "test-unsub-secret-key-12345678901234567890",
                ["Campaigns:AudienceLimitWithoutApproval"] = "500",
                ["App:PublicBaseUrl"] = "http://localhost:4500"
            })
            .Build();

    private sealed class NoOpPublisher : IEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class => Task.CompletedTask;
    }

    private sealed class MockReportingClient : IReportingClient
    {
        public Dictionary<long, (DateOnly Date, decimal Total)> FirstInvoices { get; } = [];

        public Task<List<long>> SegmentContactsAsync(Guid customerId, Guid orgId, PurchaseHistoryRule rule, CancellationToken ct) =>
            Task.FromResult(new List<long>());

        public Task<Dictionary<long, (DateOnly Date, decimal Total)>> GetFirstInvoicesAsync(
            Guid customerId, Guid orgId, List<long> contactIds, CancellationToken ct)
        {
            var result = FirstInvoices
                .Where(kvp => contactIds.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return Task.FromResult(result);
        }
    }

    private sealed class MockContactsClient : IContactsClient
    {
        public Task<IReadOnlyDictionary<long, Shared.Kernel.Documents.ContactEmail>?> EmailsAsync(
            Guid customerId, Guid orgId, IReadOnlyCollection<long> contactIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, Shared.Kernel.Documents.ContactEmail>?>(
                new Dictionary<long, Shared.Kernel.Documents.ContactEmail>());

        public Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct) => Task.FromResult(true);
        public Task<CreatedContact?> CreateAsync(NewContactRequest request, CancellationToken ct) =>
            Task.FromResult<CreatedContact?>(new CreatedContact(request.DisplayName.GetHashCode(), "C-1", request.DisplayName));
        public Task<List<ContactItemDto>> ListContactsAsync(CancellationToken ct) => Task.FromResult(new List<ContactItemDto>());
    }

    [SkippableFact]
    public async Task Conversions_CountOnlyLeadsConvertedWithinWindowAfterSend()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        var config = CreateConfig();
        var consentRepo = new EmailConsentRepository(db);
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var trackingSigner = new CampaignTrackingTokenSigner(config);
        var reportingClient = new MockReportingClient();
        var contactsClient = new MockContactsClient();
        var audienceRepo = new AudienceRepository(db, tenant, contactsClient, reportingClient, consentRepo);
        var events = new NoOpPublisher();
        var log = NullLogger<CampaignRepository>.Instance;

        var repo = new CampaignRepository(
            db, tenant, audienceRepo, consentRepo, unsubSigner, trackingSigner, events, config, log, reportingClient);

        DateTimeOffset sendTime = DateTimeOffset.UtcNow.AddDays(-40);

        // Setup leads
        // Lead 1: Converted 10 days after send (WITHIN 30-day window) -> ConvertedContactId = 101
        var lead1 = new Lead
        {
            Name = "Alice",
            Email = "alice@example.com",
            Status = LeadStatus.Converted,
            ConvertedAt = sendTime.AddDays(10),
            ConvertedContactId = 101
        };
        // Lead 2: Converted 35 days after send (OUTSIDE 30-day window) -> ConvertedContactId = 102
        var lead2 = new Lead
        {
            Name = "Bob",
            Email = "bob@example.com",
            Status = LeadStatus.Converted,
            ConvertedAt = sendTime.AddDays(35),
            ConvertedContactId = 102
        };
        // Lead 3: Converted BEFORE send (OUTSIDE window) -> ConvertedContactId = 103
        var lead3 = new Lead
        {
            Name = "Charlie",
            Email = "charlie@example.com",
            Status = LeadStatus.Converted,
            ConvertedAt = sendTime.AddDays(-5),
            ConvertedContactId = 103
        };
        // Lead 4: Not converted
        var lead4 = new Lead
        {
            Name = "David",
            Email = "david@example.com",
            Status = LeadStatus.New
        };
        db.Leads.AddRange(lead1, lead2, lead3, lead4);
        await db.SaveChangesAsync();

        // Setup first invoice in reporting for Alice (101) = 2500, Bob (102) = 5000
        reportingClient.FirstInvoices[101] = (DateOnly.FromDateTime(sendTime.AddDays(12).UtcDateTime), 2500m);
        reportingClient.FirstInvoices[102] = (DateOnly.FromDateTime(sendTime.AddDays(36).UtcDateTime), 5000m);

        // Setup campaign
        var campaign = new Campaign
        {
            Name = "Spring Promo",
            Channel = CampaignChannel.Email,
            EmailTemplateId = 1,
            AudienceId = 1,
            FromName = "Store",
            Status = CampaignStatus.Sent,
            SentAt = sendTime,
            TotalRecipients = 4,
            TotalSent = 4,
            TotalOpened = 3,
            TotalClicked = 2
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        // Setup recipients
        db.CampaignRecipients.AddRange(
            new CampaignRecipient { CampaignId = campaign.CampaignId, LeadId = lead1.LeadId, Email = lead1.Email!, Status = RecipientStatus.Sent, SentAt = sendTime },
            new CampaignRecipient { CampaignId = campaign.CampaignId, LeadId = lead2.LeadId, Email = lead2.Email!, Status = RecipientStatus.Sent, SentAt = sendTime },
            new CampaignRecipient { CampaignId = campaign.CampaignId, LeadId = lead3.LeadId, Email = lead3.Email!, Status = RecipientStatus.Sent, SentAt = sendTime },
            new CampaignRecipient { CampaignId = campaign.CampaignId, LeadId = lead4.LeadId, Email = lead4.Email!, Status = RecipientStatus.Sent, SentAt = sendTime }
        );
        await db.SaveChangesAsync();

        // Execute report
        var report = await repo.GetCampaignReportAsync(campaign.CampaignId, default);

        Assert.NotNull(report);
        Assert.Equal("Spring Promo", report.Name);
        Assert.Equal(4, report.TotalRecipients);
        Assert.Equal(4, report.TotalSent);
        Assert.Equal(3, report.TotalOpened);
        Assert.True(report.IsOpenedApproximate);
        Assert.Equal(2, report.TotalClicked);

        // Only Lead 1 (Alice) converted within the 30-day window
        Assert.Equal(1, report.TotalConversions);
        Assert.Equal(2500m, report.FirstInvoiceTotal);
        Assert.Single(report.Conversions);
        Assert.Equal(lead1.LeadId, report.Conversions[0].LeadId);
        Assert.Equal(2500m, report.Conversions[0].FirstInvoiceTotal);
    }

    [SkippableFact]
    public async Task SummaryReport_SumsMetricsAcrossAllCampaigns()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        var config = CreateConfig();
        var consentRepo = new EmailConsentRepository(db);
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var trackingSigner = new CampaignTrackingTokenSigner(config);
        var reportingClient = new MockReportingClient();
        var contactsClient = new MockContactsClient();
        var audienceRepo = new AudienceRepository(db, tenant, contactsClient, reportingClient, consentRepo);
        var events = new NoOpPublisher();
        var log = NullLogger<CampaignRepository>.Instance;

        var repo = new CampaignRepository(
            db, tenant, audienceRepo, consentRepo, unsubSigner, trackingSigner, events, config, log, reportingClient);

        var c1 = new Campaign
        {
            Name = "Campaign 1",
            Channel = CampaignChannel.Email,
            EmailTemplateId = 1,
            AudienceId = 1,
            FromName = "Store",
            Status = CampaignStatus.Sent,
            SentAt = DateTimeOffset.UtcNow.AddDays(-10),
            TotalRecipients = 10,
            TotalSent = 9,
            TotalFailed = 1,
            TotalSuppressed = 2,
            TotalOpened = 5,
            TotalClicked = 3,
            TotalUnsubscribed = 1
        };

        var c2 = new Campaign
        {
            Name = "Campaign 2",
            Channel = CampaignChannel.Email,
            EmailTemplateId = 1,
            AudienceId = 1,
            FromName = "Store",
            Status = CampaignStatus.Sent,
            SentAt = DateTimeOffset.UtcNow.AddDays(-5),
            TotalRecipients = 20,
            TotalSent = 18,
            TotalFailed = 2,
            TotalSuppressed = 4,
            TotalOpened = 8,
            TotalClicked = 4,
            TotalUnsubscribed = 2
        };

        db.Campaigns.AddRange(c1, c2);
        await db.SaveChangesAsync();

        var summary = await repo.GetCampaignsSummaryReportAsync(default);

        Assert.Equal(2, summary.TotalCampaigns);
        Assert.Equal(30, summary.TotalRecipients);
        Assert.Equal(6, summary.TotalSuppressed);
        Assert.Equal(27, summary.TotalSent);
        Assert.Equal(3, summary.TotalFailed);
        Assert.Equal(13, summary.TotalOpened);
        Assert.True(summary.IsOpenedApproximate);
        Assert.Equal(7, summary.TotalClicked);
        Assert.Equal(3, summary.TotalUnsubscribed);
        Assert.Equal(2, summary.Campaigns.Count);
    }
}
