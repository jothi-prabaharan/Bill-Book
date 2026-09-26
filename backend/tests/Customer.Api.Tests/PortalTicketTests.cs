using Customer.Api.Services;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// Support tickets in the client portal (TK-97): an internal note never
/// reaches the portal, another contact's ticket is not found, and a portal
/// ticket takes its SLA from the branch's policy.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PortalTicketTests
{
    private const long Contact = 42;
    private const long Stranger = 43;

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 6, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public PortalTicketTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task An_internal_note_never_reaches_the_portal()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        long ticket = await h.Portal.RaiseAsync(Contact, "Wrong invoice amount", "INV/26/0001 is 100 short", default);

        h.Db.TicketMessages.AddRange(
            new TicketMessage { TicketId = ticket, AuthorType = TicketAuthorType.User, Body = "Looking into it." },
            new TicketMessage { TicketId = ticket, AuthorType = TicketAuthorType.User, Body = "Customer is right, credit them.", IsInternal = true });
        await h.Db.SaveChangesAsync();
        h.Db.ChangeTracker.Clear();

        PortalTicketDetail detail = (await h.Portal.GetAsync(Contact, ticket, default))!;

        PortalTicketMessage only = Assert.Single(detail.Messages);
        Assert.Equal("Looking into it.", only.Body);
        Assert.Equal("Support", only.From);
    }

    [SkippableFact]
    public async Task A_contact_cannot_write_an_internal_note()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        long ticket = await h.Portal.RaiseAsync(Contact, "Help", null, default);

        h.Db.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket, AuthorType = TicketAuthorType.Contact, Body = "Sneaky", IsInternal = true,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => h.Db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Another_contacts_ticket_is_not_found_and_cannot_be_replied_to()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        long theirs = await h.Portal.RaiseAsync(Stranger, "Their problem", null, default);

        Assert.Null(await h.Portal.GetAsync(Contact, theirs, default));
        Assert.Equal(PortalTicketOutcome.NotFound, await h.Portal.ReplyAsync(Contact, theirs, "Hello", default));
        Assert.Empty(await h.Portal.ListAsync(Contact, default));
        Assert.Equal(0, await h.Db.TicketMessages.CountAsync(m => m.TicketId == theirs));
    }

    [SkippableFact]
    public async Task A_portal_ticket_takes_its_sla_from_the_branchs_policy_at_medium_priority()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        await new SlaPolicyService(h.Db).SeedAsync(h.OrgId, default);
        SlaPolicy medium = await h.Db.SlaPolicies.SingleAsync(p => p.Priority == TicketPriority.Medium);
        medium.ResolutionHours = 10;
        await h.Db.SaveChangesAsync();

        long ticket = await h.Portal.RaiseAsync(Contact, "Help", null, default);

        Ticket row = await h.Db.Tickets.AsNoTracking().SingleAsync(t => t.TicketId == ticket);
        Assert.Equal(TicketPriority.Medium, row.Priority);
        Assert.Equal(Now.AddHours(10), row.SlaDueAt);
    }

    [SkippableFact]
    public async Task A_reply_reopens_a_resolved_ticket_and_a_closed_one_refuses()
    {
        Harness h = await Harness.CreateAsync(_postgres);
        long resolved = await h.Portal.RaiseAsync(Contact, "Fixed?", null, default);
        long closed = await h.Portal.RaiseAsync(Contact, "Done", null, default);

        await h.Db.Tickets.Where(t => t.TicketId == resolved)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.Status, TicketStatus.Resolved).SetProperty(x => x.ResolvedAt, Now));
        await h.Db.Tickets.Where(t => t.TicketId == closed)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.Status, TicketStatus.Closed));

        Assert.Equal(PortalTicketOutcome.Ok, await h.Portal.ReplyAsync(Contact, resolved, "Still broken", default));
        Assert.Equal(PortalTicketOutcome.Closed, await h.Portal.ReplyAsync(Contact, closed, "Hello?", default));

        Ticket reopened = await h.Db.Tickets.AsNoTracking().SingleAsync(t => t.TicketId == resolved);
        Assert.Equal(TicketStatus.Open, reopened.Status);
        Assert.Null(reopened.ResolvedAt);

        TicketMessage reply = await h.Db.TicketMessages.AsNoTracking().SingleAsync(m => m.TicketId == resolved);
        Assert.Equal(TicketAuthorType.Contact, reply.AuthorType);
        Assert.False(reply.IsInternal);
    }

    private sealed record Harness(CustomerDbContext Db, Guid OrgId, PortalTicketService Portal)
    {
        public static Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            Guid orgId = Guid.NewGuid();
            CustomerDbContext db = postgres.CreateContext(Guid.NewGuid(), orgId);
            return Task.FromResult(new Harness(db, orgId, new PortalTicketService(db, new SlaPolicyService(db), new FixedClock())));
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
