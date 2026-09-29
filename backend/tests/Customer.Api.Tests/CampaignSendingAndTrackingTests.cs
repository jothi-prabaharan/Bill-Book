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
using Shared.Kernel.Interfaces;
using Shared.Kernel.Messaging;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class CampaignSendingAndTrackingTests
{
    private readonly PostgresFixture _postgres;

    public CampaignSendingAndTrackingTests(PostgresFixture postgres) => _postgres = postgres;

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Unsubscribe:SigningKey"] = "test-unsub-secret-key-12345678901234567890",
                ["CampaignTracking:SigningKey"] = "test-tracking-secret-key-12345678901234567890",
                ["Campaigns:AudienceLimitWithoutApproval"] = "5"
            })
            .Build();

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public List<object> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class StubContacts : IContactsClient
    {
        public List<ContactItemDto> Contacts { get; set; } = [];
        public Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct) => Task.FromResult(true);
        public Task<CreatedContact?> CreateAsync(NewContactRequest request, CancellationToken ct) => Task.FromResult<CreatedContact?>(null);
        public Task<List<ContactItemDto>> ListContactsAsync(CancellationToken ct) => Task.FromResult(Contacts);
    }

    private sealed class StubReporting : IReportingClient
    {
        public Task<List<long>> SegmentContactsAsync(Guid customerId, Guid orgId, PurchaseHistoryRule rule, CancellationToken ct) =>
            Task.FromResult(new List<long>());
    }

    [SkippableFact]
    public async Task An_address_appearing_as_lead_and_contact_gets_one_copy()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        var consentRepo = new EmailConsentRepository(db);

        string sharedEmail = "duplicate@example.com";

        // Add as lead
        db.Leads.Add(new Lead { Name = "Duplicate Lead", Email = sharedEmail, OrgId = orgId });
        await db.SaveChangesAsync();

        // Add as contact
        var contactsStub = new StubContacts
        {
            Contacts =
            [
                new ContactItemDto
                {
                    ContactId = 101,
                    DisplayName = "Duplicate Contact",
                    Email = sharedEmail,
                    IsCustomer = true,
                    IsActive = true
                }
            ]
        };

        var audienceRepo = new AudienceRepository(db, tenant, contactsStub, new StubReporting(), consentRepo);
        var preview = await audienceRepo.ResolvePreviewAsync(new AudienceRules
        {
            Leads = new LeadRule(),
            Contacts = new ContactRule { IsCustomer = true }
        }, CancellationToken.None);

        Assert.Equal(1, preview.TotalSubscribedRecipients);
        Assert.Single(preview.SampleRecipients);
        Assert.Equal(sharedEmail, preview.SampleRecipients[0].Email);
    }

    [SkippableFact]
    public async Task A_campaign_sent_twice_sends_each_recipient_once()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        var config = CreateConfig();
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var trackingSigner = new CampaignTrackingTokenSigner(config);
        var events = new RecordingEventPublisher();
        var consentRepo = new EmailConsentRepository(db);

        // Add subscribed lead
        db.Leads.Add(new Lead { Name = "Recipient 1", Email = "rec1@example.com", OrgId = orgId });
        await db.SaveChangesAsync();

        var audienceRepo = new AudienceRepository(db, tenant, new StubContacts(), new StubReporting(), consentRepo);
        var audience = await audienceRepo.CreateAsync(new SaveAudienceRequest
        {
            Name = "All Leads",
            Rules = new AudienceRules { Leads = new LeadRule() }
        }, CancellationToken.None);

        var templateRepo = new EmailTemplateRepository(db);
        var template = await templateRepo.CreateAsync(new SaveEmailTemplateRequest
        {
            Name = "Test Template",
            Subject = "Subject",
            HtmlBody = "<p>Hello <a href=\"https://google.com\">Click here</a> {{unsubscribe}}</p>"
        }, CancellationToken.None);

        var campaignRepo = new CampaignRepository(
            db,
            tenant,
            audienceRepo,
            consentRepo,
            unsubSigner,
            trackingSigner,
            events,
            config,
            NullLogger<CampaignRepository>.Instance);

        var campaign = await campaignRepo.CreateAsync(new SaveCampaignRequest
        {
            Name = "Campaign 1",
            EmailTemplateId = template.EmailTemplateId,
            AudienceId = audience.AudienceId,
            FromName = "Store"
        }, CancellationToken.None);

        await campaignRepo.ScheduleAsync(campaign.CampaignId, new ScheduleCampaignRequest
        {
            ScheduledAt = DateTimeOffset.UtcNow
        }, hasApprovePermission: true, CancellationToken.None);

        // Send first time
        await campaignRepo.ExecuteCampaignSendAsync(campaign.CampaignId, CancellationToken.None);

        Assert.Single(events.Published);
        var firstEmail = Assert.IsType<EmailRequested>(events.Published[0]);
        Assert.StartsWith($"cmp-{campaign.CampaignId}-", firstEmail.MessageId);
        Assert.Equal("rec1@example.com", firstEmail.ToEmail);
        Assert.Contains("List-Unsubscribe", firstEmail.Headers.Keys);

        // Send second time (simulating worker restart)
        await campaignRepo.ExecuteCampaignSendAsync(campaign.CampaignId, CancellationToken.None);

        // No new emails should be queued because recipients are already processed
        Assert.Single(events.Published);
    }

    [SkippableFact]
    public async Task A_click_is_recorded_and_redirected()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        var config = CreateConfig();
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var trackingSigner = new CampaignTrackingTokenSigner(config);
        var events = new RecordingEventPublisher();
        var consentRepo = new EmailConsentRepository(db);
        var audienceRepo = new AudienceRepository(db, tenant, new StubContacts(), new StubReporting(), consentRepo);

        var campaignRepo = new CampaignRepository(
            db,
            tenant,
            audienceRepo,
            consentRepo,
            unsubSigner,
            trackingSigner,
            events,
            config,
            NullLogger<CampaignRepository>.Instance);

        var link = new CampaignLink
        {
            CampaignId = 1,
            OriginalUrl = "https://example.com/products/sale",
            TrackingToken = "token123",
            ClickCount = 0,
            OrgId = orgId
        };
        db.CampaignLinks.Add(link);
        await db.SaveChangesAsync();

        string? redirectUrl = await campaignRepo.RecordClickAsync("token123", CancellationToken.None);

        Assert.Equal("https://example.com/products/sale", redirectUrl);

        var updatedLink = await db.CampaignLinks.FirstAsync(l => l.TrackingToken == "token123");
        Assert.Equal(1, updatedLink.ClickCount);
        Assert.NotNull(updatedLink.FirstClickedAt);
    }

    [SkippableFact]
    public async Task Hard_bounce_marks_consent_as_bounced()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        var config = CreateConfig();
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var trackingSigner = new CampaignTrackingTokenSigner(config);
        var events = new RecordingEventPublisher();
        var consentRepo = new EmailConsentRepository(db);
        var audienceRepo = new AudienceRepository(db, tenant, new StubContacts(), new StubReporting(), consentRepo);

        var campaignRepo = new CampaignRepository(
            db,
            tenant,
            audienceRepo,
            consentRepo,
            unsubSigner,
            trackingSigner,
            events,
            config,
            NullLogger<CampaignRepository>.Instance);

        string bounceEmail = "bounced@example.com";
        var recipient = new CampaignRecipient
        {
            CampaignId = 10,
            Email = bounceEmail,
            Status = RecipientStatus.Sent,
            OrgId = orgId
        };
        db.CampaignRecipients.Add(recipient);
        await db.SaveChangesAsync();

        string messageId = $"cmp-10-{recipient.CampaignRecipientId}";

        // Handle delivery failure with hard bounce
        await campaignRepo.HandleDeliveryOutcomeAsync(
            messageId,
            delivered: false,
            isHardBounce: true,
            failureReason: "550 5.1.1 User unknown",
            CancellationToken.None);

        var consent = await consentRepo.GetConsentAsync(bounceEmail, CancellationToken.None);
        Assert.NotNull(consent);
        Assert.Equal(ConsentStatus.Bounced, consent.Status);
        Assert.Equal(ConsentSource.Bounce, consent.Source);
    }

    [SkippableFact]
    public async Task Audience_size_above_limit_requires_crm_approve_permission()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);
        var config = CreateConfig();
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var trackingSigner = new CampaignTrackingTokenSigner(config);
        var events = new RecordingEventPublisher();
        var consentRepo = new EmailConsentRepository(db);

        // Seed 10 leads (limit is 5 in config)
        for (int i = 0; i < 10; i++)
        {
            db.Leads.Add(new Lead { Name = $"Lead {i}", Email = $"lead{i}@example.com", OrgId = orgId });
        }
        await db.SaveChangesAsync();

        var audienceRepo = new AudienceRepository(db, tenant, new StubContacts(), new StubReporting(), consentRepo);
        var audience = await audienceRepo.CreateAsync(new SaveAudienceRequest
        {
            Name = "10 Leads",
            Rules = new AudienceRules { Leads = new LeadRule() }
        }, CancellationToken.None);

        var templateRepo = new EmailTemplateRepository(db);
        var template = await templateRepo.CreateAsync(new SaveEmailTemplateRequest
        {
            Name = "Template",
            Subject = "Subject",
            HtmlBody = "Body {{unsubscribe}}"
        }, CancellationToken.None);

        var campaignRepo = new CampaignRepository(
            db,
            tenant,
            audienceRepo,
            consentRepo,
            unsubSigner,
            trackingSigner,
            events,
            config,
            NullLogger<CampaignRepository>.Instance);

        var campaign = await campaignRepo.CreateAsync(new SaveCampaignRequest
        {
            Name = "Big Campaign",
            EmailTemplateId = template.EmailTemplateId,
            AudienceId = audience.AudienceId,
            FromName = "Store"
        }, CancellationToken.None);

        // Scheduling without crm.approve should fail
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            campaignRepo.ScheduleAsync(campaign.CampaignId, new ScheduleCampaignRequest
            {
                ScheduledAt = DateTimeOffset.UtcNow
            }, hasApprovePermission: false, CancellationToken.None));

        Assert.Contains("crm.approve", ex.Message);

        // Scheduling with crm.approve should succeed
        var scheduled = await campaignRepo.ScheduleAsync(campaign.CampaignId, new ScheduleCampaignRequest
        {
            ScheduledAt = DateTimeOffset.UtcNow
        }, hasApprovePermission: true, CancellationToken.None);

        Assert.Equal(CampaignStatus.Scheduled, scheduled.Status);
    }
}
