using Customer.Api.Services;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Customer.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// Each branch's SLA policies (D-18, TK-18): seeded once per priority, and the
/// source of a ticket's <c>SlaDueAt</c> in place of the table the ticket
/// controller hard-coded for every branch alike.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SlaPolicyTests
{
    private static readonly DateTimeOffset Raised = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public SlaPolicyTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Seeding_adds_one_policy_per_priority_and_seeding_again_adds_nothing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var service = new SlaPolicyService(db);

        Assert.Equal(4, await service.SeedAsync(orgId, default));
        Assert.Equal(0, await service.SeedAsync(orgId, default));

        List<SlaPolicy> rows = await db.SlaPolicies.AsNoTracking().ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.Equal(
            Enum.GetValues<TicketPriority>().OrderBy(p => p),
            rows.Select(r => r.Priority).OrderBy(p => p));
    }

    [SkippableFact]
    public async Task A_reseed_keeps_the_hours_a_branch_changed_and_adds_only_what_is_missing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var service = new SlaPolicyService(db);

        await service.SeedAsync(orgId, default);

        SlaPolicy high = await db.SlaPolicies.SingleAsync(p => p.Priority == TicketPriority.High);
        high.ResolutionHours = 24;
        db.SlaPolicies.Remove(await db.SlaPolicies.SingleAsync(p => p.Priority == TicketPriority.Low));
        await db.SaveChangesAsync();

        Assert.Equal(1, await service.SeedAsync(orgId, default));

        Assert.Equal(24, (await db.SlaPolicies.AsNoTracking().SingleAsync(p => p.Priority == TicketPriority.High)).ResolutionHours);
        Assert.Equal(168, (await db.SlaPolicies.AsNoTracking().SingleAsync(p => p.Priority == TicketPriority.Low)).ResolutionHours);
    }

    [SkippableFact]
    public async Task A_tickets_due_date_follows_its_own_branchs_policy()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid branchA = Guid.NewGuid();
        Guid branchB = Guid.NewGuid();

        await using CustomerDbContext a = _postgres.CreateContext(customerId, branchA);
        await using CustomerDbContext b = _postgres.CreateContext(customerId, branchB);

        await new SlaPolicyService(a).SeedAsync(branchA, default);
        await new SlaPolicyService(b).SeedAsync(branchB, default);

        // Branch B promises a High ticket in a day rather than eight hours.
        SlaPolicy bHigh = await b.SlaPolicies.SingleAsync(p => p.Priority == TicketPriority.High);
        bHigh.ResolutionHours = 24;
        await b.SaveChangesAsync();

        Assert.Equal(Raised.AddHours(8), await new SlaPolicyService(a).DueAtAsync(TicketPriority.High, Raised, default));
        Assert.Equal(Raised.AddHours(24), await new SlaPolicyService(b).DueAtAsync(TicketPriority.High, Raised, default));
    }

    [SkippableFact]
    public async Task A_branch_not_yet_seeded_gets_the_default_hours_rather_than_no_due_date()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using CustomerDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var service = new SlaPolicyService(db);

        Assert.Equal(Raised.AddHours(2), await service.DueAtAsync(TicketPriority.Urgent, Raised, default));
        Assert.Equal(Raised.AddDays(7), await service.DueAtAsync(TicketPriority.Low, Raised, default));
    }

    [Fact]
    public void The_defaults_are_the_hours_the_ticket_controller_used_to_hard_code()
    {
        // Urgent 2 h, High 8 h, Medium 2 days, Low 7 days — so no branch sees a
        // change until it edits its own policy.
        Assert.Equal(2, SlaPolicyService.DefaultResolutionHours(TicketPriority.Urgent));
        Assert.Equal(8, SlaPolicyService.DefaultResolutionHours(TicketPriority.High));
        Assert.Equal(48, SlaPolicyService.DefaultResolutionHours(TicketPriority.Medium));
        Assert.Equal(168, SlaPolicyService.DefaultResolutionHours(TicketPriority.Low));

        Assert.All(SlaPolicySeed.Defaults, d => Assert.True(d.ResponseHours <= d.ResolutionHours));
        Assert.Equal(Enum.GetValues<TicketPriority>().Length, SlaPolicySeed.Defaults.Count);
    }
}
