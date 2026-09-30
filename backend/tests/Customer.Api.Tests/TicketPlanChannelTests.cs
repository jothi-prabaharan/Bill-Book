using Customer.Api.Controllers;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Customer.Repository.Clients;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class TicketPlanChannelTests
{
    private readonly PostgresFixture _postgres;

    public TicketPlanChannelTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class StubContacts : IContactsClient
    {
        public HashSet<long> Mine { get; init; } = [];

        public Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct) =>
            Task.FromResult(Mine.Contains(contactId));

        public Task<CreatedContact?> CreateAsync(NewContactRequest request, CancellationToken ct) =>
            Task.FromResult<CreatedContact?>(new CreatedContact(9001, "CON-9001", request.DisplayName));

        public Task<List<ContactItemDto>> ListContactsAsync(CancellationToken ct) =>
            Task.FromResult(new List<ContactItemDto>());
    }

    private static TicketsController Tickets(
        CustomerDbContext db, Guid customerId, Guid orgId, IContactsClient contacts, PlanTier planTier) =>
        new(
            db,
            new TenantContext { CustomerId = customerId, OrgId = orgId, PlanTier = planTier },
            contacts,
            new SlaPolicyRepository(db),
            TimeProvider.System);

    [Fact]
    public void TicketChannelRules_identifies_call_channels()
    {
        Assert.True(TicketChannelRules.IsCallChannel(TicketChannel.Call));
        Assert.True(TicketChannelRules.IsCallChannel(TicketChannel.Phone));
        Assert.False(TicketChannelRules.IsCallChannel(TicketChannel.Web));
        Assert.False(TicketChannelRules.IsCallChannel(TicketChannel.Email));
        Assert.False(TicketChannelRules.IsCallChannel(TicketChannel.Chat));
    }

    [Theory]
    [InlineData(PlanTier.Trial, TicketChannel.Web, true)]
    [InlineData(PlanTier.Trial, TicketChannel.Email, true)]
    [InlineData(PlanTier.Trial, TicketChannel.Chat, true)]
    [InlineData(PlanTier.Trial, TicketChannel.Call, false)]
    [InlineData(PlanTier.Standard, TicketChannel.Call, false)]
    [InlineData(PlanTier.Pro, TicketChannel.Call, true)]
    [InlineData(PlanTier.Pro, TicketChannel.Web, true)]
    [InlineData(PlanTier.Elite, TicketChannel.Call, true)]
    public void TicketChannelRules_restricts_call_channel_by_plan(
        PlanTier tier, TicketChannel channel, bool expectedAllowed)
    {
        Assert.Equal(expectedAllowed, TicketChannelRules.IsChannelAllowed(tier, channel));
    }

    [SkippableFact]
    public async Task Standard_or_basic_plan_cannot_create_ticket_with_call_channel()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        TicketsController controller = Tickets(
            db, customerId, orgId, new StubContacts { Mine = [100] }, PlanTier.Standard);

        IActionResult result = await controller.Create(
            new SaveTicketRequest
            {
                ContactId = 100,
                Subject = "Need phone assistance",
                Channel = TicketChannel.Call,
            },
            CancellationToken.None);

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.False(await db.Tickets.AnyAsync(t => t.Subject == "Need phone assistance"));
    }

    [SkippableFact]
    public async Task Standard_or_basic_plan_can_create_ticket_with_text_channel()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        TicketsController controller = Tickets(
            db, customerId, orgId, new StubContacts { Mine = [100] }, PlanTier.Basic);

        IActionResult result = await controller.Create(
            new SaveTicketRequest
            {
                ContactId = 100,
                Subject = "Web inquiry",
                Channel = TicketChannel.Web,
            },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        Ticket created = await db.Tickets.FirstAsync(t => t.Subject == "Web inquiry");
        Assert.Equal(TicketChannel.Web, created.Channel);
    }

    [SkippableFact]
    public async Task Pro_plan_can_create_ticket_with_call_channel()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        TicketsController controller = Tickets(
            db, customerId, orgId, new StubContacts { Mine = [100] }, PlanTier.Pro);

        IActionResult result = await controller.Create(
            new SaveTicketRequest
            {
                ContactId = 100,
                Subject = "Urgent call requested",
                Channel = TicketChannel.Call,
            },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        Ticket created = await db.Tickets.FirstAsync(t => t.Subject == "Urgent call requested");
        Assert.Equal(TicketChannel.Call, created.Channel);
    }

    [SkippableFact]
    public async Task Updating_channel_to_call_on_basic_plan_is_rejected()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        var ticket = new Ticket
        {
            ContactId = 100,
            Subject = "Original ticket",
            Channel = TicketChannel.Web,
            Status = TicketStatus.Open,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        TicketsController controller = Tickets(
            db, customerId, orgId, new StubContacts { Mine = [100] }, PlanTier.Basic);

        IActionResult result = await controller.Update(
            ticket.TicketId,
            new SaveTicketRequest
            {
                ContactId = 100,
                Subject = "Updated ticket",
                Channel = TicketChannel.Call,
            },
            CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);

        // Verify channel was not modified
        Ticket unchanged = await db.Tickets.FirstAsync(t => t.TicketId == ticket.TicketId);
        Assert.Equal(TicketChannel.Web, unchanged.Channel);
    }
}
