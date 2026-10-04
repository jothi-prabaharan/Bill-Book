using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Customer.Repository.Clients;
using Customer.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// Service plans (TK-140). The card's Done-when line: a plan due in N days
/// emails the customer once, and a rerun sends nothing — the run hands the
/// worker the same fixed message id each time, and the worker's
/// <c>EmailRequestHandler</c> sends a message id once.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ServicePlanTests
{
    private const long Customer = 7;
    private const long Silent = 8;
    private const long Unsubscribed = 9;

    private readonly PostgresFixture _postgres;

    public ServicePlanTests(PostgresFixture postgres) => _postgres = postgres;

    [Theory]
    [InlineData(ServicePlanInterval.Monthly, 6, "2026-04-10")]
    [InlineData(ServicePlanInterval.Yearly, 1, "2027-10-10")]
    [InlineData(ServicePlanInterval.Weekly, 2, "2026-10-24")]
    public void The_next_service_is_one_interval_on(ServicePlanInterval interval, int every, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), ServicePlanRepository.NextAfter(new DateOnly(2026, 10, 10), interval, every));

    [SkippableFact]
    public async Task A_plan_due_in_three_days_gives_one_reminder_and_a_rerun_gives_the_same_message_id()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.PlanAsync("AC service", Customer, due: h.Today.AddDays(3), leadDays: 7);

        RunServiceRemindersResponse first = await h.Plans.RemindersAsync(h.Today, default);
        RunServiceRemindersResponse second = await h.Plans.RemindersAsync(h.Today, default);

        ServiceReminderMessage message = Assert.Single(first.Messages);
        Assert.Equal("ravi@example.com", message.ToEmail);
        Assert.Contains("AC service", message.Subject);
        Assert.Contains("Ravi", message.HtmlBody);
        Assert.Equal(message.MessageId, Assert.Single(second.Messages).MessageId);
    }

    [SkippableFact]
    public async Task A_plan_outside_its_lead_days_or_paused_gives_no_reminder()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.PlanAsync("Filter change", Customer, due: h.Today.AddDays(20), leadDays: 7);
        long paused = await h.PlanAsync("Deep clean", Customer, due: h.Today.AddDays(1), leadDays: 7);
        await h.Plans.PauseAsync(paused, default);

        Assert.Empty((await h.Plans.RemindersAsync(h.Today, default)).Messages);
    }

    [SkippableFact]
    public async Task Unsubscribed_customers_and_customers_without_an_email_are_counted_not_sent()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.PlanAsync("AC service", Silent, due: h.Today.AddDays(1), leadDays: 7);
        await h.PlanAsync("AC service", Unsubscribed, due: h.Today.AddDays(1), leadDays: 7);

        h.Db.EmailConsents.Add(new EmailConsent
        {
            Email = "gone@example.com",
            Status = ConsentStatus.Unsubscribed,
            Source = ConsentSource.Staff,
            ChangedAt = DateTimeOffset.UtcNow,
        });
        await h.Db.SaveChangesAsync();

        RunServiceRemindersResponse run = await h.Plans.RemindersAsync(h.Today, default);

        Assert.Empty(run.Messages);
        Assert.Equal(1, run.NoEmail);
        Assert.Equal(1, run.Unsubscribed);
    }

    [SkippableFact]
    public async Task The_branch_template_words_the_reminder()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        await h.Plans.SeedTemplateAsync(default);
        EmailTemplate template = await h.Db.EmailTemplates.SingleAsync(t => t.Name == ServicePlanRepository.TemplateName);
        template.Subject = "Time for your {{plan}}";
        await h.Db.SaveChangesAsync();

        await h.PlanAsync("RO service", Customer, due: h.Today.AddDays(2), leadDays: 7);

        Assert.Equal("Time for your RO service", Assert.Single((await h.Plans.RemindersAsync(h.Today, default)).Messages).Subject);
        Assert.Equal(0, await h.Plans.SeedTemplateAsync(default));
    }

    [SkippableFact]
    public async Task Completing_a_service_moves_the_next_due_date_on_from_the_day_it_was_done()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long plan = await h.PlanAsync("AC service", Customer, due: h.Today, leadDays: 7);

        ServicePlanResult done = await h.Plans.CompleteAsync(plan, new CompleteServiceRequest { CompletedOn = h.Today.AddDays(-2) }, default);

        Assert.Equal(ServicePlanOutcome.Ok, done.Outcome);
        ServicePlanView? after = await h.Plans.GetAsync(plan, default);
        Assert.Equal(h.Today.AddDays(-2).AddMonths(6), after!.NextDueDate);
        Assert.Equal(h.Today.AddDays(-2), after.LastServicedOn);
        Assert.Equal("Active", after.Status);
    }

    [SkippableFact]
    public async Task A_plan_whose_next_service_falls_past_its_end_date_ends_and_a_future_completion_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long plan = await h.PlanAsync("AC service", Customer, due: h.Today, leadDays: 7, end: h.Today.AddMonths(3));

        ServicePlanResult future = await h.Plans.CompleteAsync(plan, new CompleteServiceRequest { CompletedOn = h.Today.AddDays(1) }, default);
        Assert.Equal(ServicePlanOutcome.Invalid, future.Outcome);

        await h.Plans.CompleteAsync(plan, new CompleteServiceRequest { CompletedOn = h.Today }, default);

        Assert.Equal("Ended", (await h.Plans.GetAsync(plan, default))!.Status);
    }

    [SkippableFact]
    public async Task Raising_a_visit_opens_one_ticket_until_it_is_resolved()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        long plan = await h.PlanAsync("AC service", Customer, due: h.Today.AddDays(2), leadDays: 7);

        ServicePlanResult raised = await h.Plans.RaiseTicketAsync(plan, default);
        Assert.Equal(ServicePlanOutcome.Ok, raised.Outcome);

        Ticket ticket = await h.Db.Tickets.AsNoTracking().SingleAsync(t => t.TicketId == raised.Id);
        Assert.Equal(Customer, ticket.ContactId);
        Assert.StartsWith("Service due: AC service", ticket.Subject);

        Assert.Equal(ServicePlanOutcome.Conflict, (await h.Plans.RaiseTicketAsync(plan, default)).Outcome);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required CustomerDbContext Db { get; init; }
        public required ServicePlanRepository Plans { get; init; }
        public DateOnly Today { get; } = DateOnly.FromDateTime(DateTime.Now);

        public async Task<long> PlanAsync(string name, long contactId, DateOnly due, int leadDays, DateOnly? end = null)
        {
            ServicePlanResult created = await Plans.CreateAsync(new SaveServicePlanRequest
            {
                Name = name,
                ContactId = contactId,
                Interval = ServicePlanInterval.Monthly,
                Every = 6,
                StartDate = due.AddYears(-1),
                NextDueDate = due,
                EndDate = end,
                LeadDays = leadDays,
            }, default);

            Assert.Equal(ServicePlanOutcome.Ok, created.Outcome);
            return created.Id;
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
            CustomerDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, tenant.OrgId!.Value);

            return new Harness
            {
                Db = db,
                Plans = new ServicePlanRepository(db, tenant, new Contacts(), new SlaPolicyRepository(db), TimeProvider.System),
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    /// <summary>Three customers: one with an address, one without, one who unsubscribed.</summary>
    private sealed class Contacts : IContactsClient
    {
        public Task<IReadOnlyDictionary<long, ContactEmail>?> EmailsAsync(
            Guid customerId, Guid orgId, IReadOnlyCollection<long> contactIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, ContactEmail>?>(new Dictionary<long, ContactEmail>
            {
                [Customer] = new(Customer, "ravi@example.com", "Ravi"),
                [Unsubscribed] = new(Unsubscribed, "Gone@Example.com", "Gone"),
            });

        public Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct) => Task.FromResult(true);

        public Task<CreatedContact?> CreateAsync(NewContactRequest request, CancellationToken ct) =>
            Task.FromResult<CreatedContact?>(null);

        public Task<List<ContactItemDto>> ListContactsAsync(CancellationToken ct) => Task.FromResult(new List<ContactItemDto>());
    }
}
