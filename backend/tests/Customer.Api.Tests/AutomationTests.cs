using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository;
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
public sealed class AutomationTests
{
    private readonly PostgresFixture _postgres;

    public AutomationTests(PostgresFixture postgres) => _postgres = postgres;

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Unsubscribe:SigningKey"] = "test-unsub-secret-key-12345678901234567890",
            })
            .Build();

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public List<EmailRequested> Emails { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            if (@event is EmailRequested email)
            {
                Emails.Add(email);
            }
            return Task.CompletedTask;
        }
    }

    [SkippableFact]
    public async Task Automation_LeadConvertedDuringWait_ExitsBeforeNextSend()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };

        await using var db = _postgres.CreateContext(customerId, orgId);
        var consentRepo = new EmailConsentRepository(db);
        var config = CreateConfig();
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var events = new RecordingEventPublisher();
        var log = NullLogger<AutomationRepository>.Instance;

        var repo = new AutomationRepository(db, tenant, consentRepo, unsubSigner, events, config, log);

        // 1. Setup email templates
        var welcomeTpl = new EmailTemplate
        {
            Name = "Welcome Email",
            Subject = "Welcome!",
            HtmlBody = "<p>Welcome {{first_name}}</p>",
            IsActive = true
        };
        var followupTpl = new EmailTemplate
        {
            Name = "Follow-up Email",
            Subject = "Following up",
            HtmlBody = "<p>Checking in {{first_name}}</p>",
            IsActive = true
        };
        db.EmailTemplates.AddRange(welcomeTpl, followupTpl);
        await db.SaveChangesAsync();

        // 2. Setup 3-step automation: Send (Welcome), Wait (3 days), Send (Follow-up)
        var auto = await repo.CreateAsync(new SaveAutomationRequest
        {
            Name = "Website Onboarding",
            TriggerType = AutomationTriggerType.LeadCreated,
            TriggerSource = "Website",
            IsActive = true,
            Steps =
            [
                new SaveAutomationStepRequest { Sequence = 1, Kind = AutomationStepKind.Send, EmailTemplateId = welcomeTpl.EmailTemplateId },
                new SaveAutomationStepRequest { Sequence = 2, Kind = AutomationStepKind.Wait, WaitDays = 3 },
                new SaveAutomationStepRequest { Sequence = 3, Kind = AutomationStepKind.Send, EmailTemplateId = followupTpl.EmailTemplateId }
            ]
        }, default);

        // 3. Lead created
        var lead = new Lead
        {
            Name = "Alice",
            Email = "alice@example.com",
            Source = LeadSource.Website,
            Status = LeadStatus.New
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        await repo.TriggerLeadCreatedAsync(lead.LeadId, lead.Email, lead.Name, lead.Source, default);

        // 4. Initial worker tick: should send Step 1 and schedule Step 3 after 3-day wait
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await repo.AdvanceDueEnrolmentsAsync(now, default);

        Assert.Single(events.Emails);
        Assert.Equal("Welcome!", events.Emails[0].Subject);
        Assert.StartsWith("aut-", events.Emails[0].MessageId);

        var enrolment = await db.AutomationEnrolments.SingleAsync(e => e.LeadId == lead.LeadId);
        Assert.Equal(AutomationEnrolmentStatus.Active, enrolment.Status);
        Assert.Equal(3, enrolment.CurrentStep);
        Assert.True(enrolment.NextRunAt >= now.AddDays(2.9));

        // 5. During the wait, lead converts to a customer!
        lead.Status = LeadStatus.Converted;
        lead.ConvertedAt = now.AddDays(1);
        await db.SaveChangesAsync();

        await repo.TriggerLeadStatusChangedAsync(lead.LeadId, LeadStatus.Converted, default);

        enrolment = await db.AutomationEnrolments.SingleAsync(e => e.LeadId == lead.LeadId);
        Assert.Equal(AutomationEnrolmentStatus.Exited, enrolment.Status);
        Assert.Equal("Converted", enrolment.ExitReason);

        // 6. 3 days pass, worker runs again: should NOT send follow-up
        events.Emails.Clear();
        await repo.AdvanceDueEnrolmentsAsync(now.AddDays(4), default);

        Assert.Empty(events.Emails);
    }

    [SkippableFact]
    public async Task Automation_WorkerRestart_DoesNotSendStepTwice()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };

        await using var db = _postgres.CreateContext(customerId, orgId);
        var consentRepo = new EmailConsentRepository(db);
        var config = CreateConfig();
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var events = new RecordingEventPublisher();
        var log = NullLogger<AutomationRepository>.Instance;

        var repo = new AutomationRepository(db, tenant, consentRepo, unsubSigner, events, config, log);

        var tpl = new EmailTemplate { Name = "Promo", Subject = "Promo", HtmlBody = "Hello", IsActive = true };
        db.EmailTemplates.Add(tpl);
        await db.SaveChangesAsync();

        var auto = await repo.CreateAsync(new SaveAutomationRequest
        {
            Name = "One Shot",
            TriggerType = AutomationTriggerType.LeadCreated,
            IsActive = true,
            Steps = [new SaveAutomationStepRequest { Sequence = 1, Kind = AutomationStepKind.Send, EmailTemplateId = tpl.EmailTemplateId }]
        }, default);

        var lead = new Lead { Name = "Bob", Email = "bob@example.com", Source = LeadSource.Referral };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        await repo.TriggerLeadCreatedAsync(lead.LeadId, lead.Email, lead.Name, lead.Source, default);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        await repo.AdvanceDueEnrolmentsAsync(now, default);

        Assert.Single(events.Emails);
        var enrolment = await db.AutomationEnrolments.SingleAsync(e => e.LeadId == lead.LeadId);
        Assert.Equal(AutomationEnrolmentStatus.Completed, enrolment.Status);

        // Simulated worker restart or second run:
        events.Emails.Clear();
        await repo.AdvanceDueEnrolmentsAsync(now, default);

        Assert.Empty(events.Emails);
    }

    [SkippableFact]
    public async Task Automation_UnsubscribedRecipient_ExitsWithoutSending()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };

        await using var db = _postgres.CreateContext(customerId, orgId);
        var consentRepo = new EmailConsentRepository(db);
        var config = CreateConfig();
        var unsubSigner = new UnsubscribeTokenSigner(config);
        var events = new RecordingEventPublisher();
        var log = NullLogger<AutomationRepository>.Instance;

        var repo = new AutomationRepository(db, tenant, consentRepo, unsubSigner, events, config, log);

        var tpl = new EmailTemplate { Name = "Update", Subject = "Update", HtmlBody = "Hi", IsActive = true };
        db.EmailTemplates.Add(tpl);
        await db.SaveChangesAsync();

        var auto = await repo.CreateAsync(new SaveAutomationRequest
        {
            Name = "Series",
            TriggerType = AutomationTriggerType.LeadCreated,
            IsActive = true,
            Steps =
            [
                new SaveAutomationStepRequest { Sequence = 1, Kind = AutomationStepKind.Wait, WaitDays = 1 },
                new SaveAutomationStepRequest { Sequence = 2, Kind = AutomationStepKind.Send, EmailTemplateId = tpl.EmailTemplateId }
            ]
        }, default);

        var lead = new Lead { Name = "Charlie", Email = "charlie@example.com", Source = LeadSource.Other };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        await repo.TriggerLeadCreatedAsync(lead.LeadId, lead.Email, lead.Name, lead.Source, default);

        // Charlie unsubscribes before the wait is over
        await consentRepo.RecordConsentAsync("charlie@example.com", ConsentStatus.Unsubscribed, ConsentSource.Staff, default);

        // Wait elapses
        await repo.AdvanceDueEnrolmentsAsync(DateTimeOffset.UtcNow.AddDays(2), default);

        Assert.Empty(events.Emails);
        var enrolment = await db.AutomationEnrolments.SingleAsync(e => e.LeadId == lead.LeadId);
        Assert.Equal(AutomationEnrolmentStatus.Exited, enrolment.Status);
        Assert.Equal("Unsubscribed", enrolment.ExitReason);
    }
}
