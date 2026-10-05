using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Repository;
using Customer.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>Loyalty points through their repository (TK-166): earned once, taken back once, and shown.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class LoyaltyTests
{
    private readonly PostgresFixture _postgres;

    public LoyaltyTests(PostgresFixture postgres) => _postgres = postgres;

    private (CustomerDbContext Db, LoyaltyRepository Loyalty, Guid Customer, Guid Org) Arrange()
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        CustomerDbContext db = _postgres.CreateContext(customer, org);
        return (db, new LoyaltyRepository(db, TimeProvider.System), customer, org);
    }

    private static EarnLoyaltyRequest Earn(long invoiceId, int points, long contactId = 42) => new()
    {
        ContactId = contactId,
        InvoiceId = invoiceId,
        DocumentNo = $"INV/{invoiceId}",
        Points = points,
    };

    [SkippableFact]
    public async Task A_posted_bill_earns_its_contact_points_once()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, loyalty, _, _) = Arrange();
        await using (db)
        {
            LoyaltyPostingResult first = await loyalty.EarnAsync(Earn(101, 12), default);
            LoyaltyPostingResult again = await loyalty.EarnAsync(Earn(101, 12), default);

            Assert.Equal(12, first.Balance);
            Assert.False(first.AlreadyRecorded);
            Assert.True(again.AlreadyRecorded);
            Assert.Equal(12, again.Balance);
            Assert.Equal(1, await db.LoyaltyTransactions.CountAsync());
        }
    }

    [SkippableFact]
    public async Task A_void_takes_the_points_back_once_and_the_history_shows_both()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, loyalty, _, _) = Arrange();
        await using (db)
        {
            await loyalty.EarnAsync(Earn(101, 12), default);
            await loyalty.EarnAsync(Earn(102, 5), default);

            LoyaltyPostingResult? reversed = await loyalty.ReverseAsync(101, default);
            LoyaltyPostingResult? again = await loyalty.ReverseAsync(101, default);

            Assert.Equal(-12, reversed!.Points);
            Assert.Equal(5, reversed.Balance);
            Assert.True(again!.AlreadyRecorded);

            LoyaltyAccountView view = await loyalty.GetAsync(42, default);
            Assert.Equal(5, view.PointsBalance);
            Assert.Equal(3, view.Transactions.Count);
            Assert.Equal(LoyaltyTransactionKind.Reversed, view.Transactions[0].Kind);
            Assert.Equal("INV/101", view.Transactions[0].DocumentNo);
            Assert.Equal(new[] { -12, 5, 12 }.Order(), view.Transactions.Select(t => t.Points).Order());
        }
    }

    [SkippableFact]
    public async Task Reversing_a_bill_that_earned_nothing_changes_nothing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, loyalty, _, _) = Arrange();
        await using (db)
        {
            Assert.Null(await loyalty.ReverseAsync(999, default));
            Assert.Empty(await db.LoyaltyTransactions.ToListAsync());
        }
    }

    [SkippableFact]
    public async Task A_contact_who_never_earned_has_a_zero_balance_and_points_stay_with_each_contact()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, loyalty, _, _) = Arrange();
        await using (db)
        {
            await loyalty.EarnAsync(Earn(101, 12, contactId: 42), default);
            await loyalty.EarnAsync(Earn(102, 7, contactId: 43), default);

            Assert.Equal(12, (await loyalty.GetAsync(42, default)).PointsBalance);
            Assert.Equal(7, (await loyalty.GetAsync(43, default)).PointsBalance);

            LoyaltyAccountView nobody = await loyalty.GetAsync(44, default);
            Assert.Equal(0, nobody.PointsBalance);
            Assert.Empty(nobody.Transactions);
        }
    }

    [SkippableFact]
    public async Task Another_branch_sees_none_of_these_points()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        var (db, loyalty, customer, _) = Arrange();
        await using (db)
        {
            await loyalty.EarnAsync(Earn(101, 12), default);
        }

        await using CustomerDbContext other = _postgres.CreateContext(customer, Guid.NewGuid());
        Assert.Equal(0, (await new LoyaltyRepository(other, TimeProvider.System).GetAsync(42, default)).PointsBalance);
    }
}
