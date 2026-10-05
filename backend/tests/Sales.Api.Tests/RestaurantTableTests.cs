using Microsoft.EntityFrameworkCore;
using Sales.Entity.Models;
using Sales.Repository;
using Sales.Repository.Repositories;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>The table rules (TK-145), pure.</summary>
public sealed class RestaurantTableRulesTests
{
    [Theory]
    [InlineData(TableStatus.Free, TableStatus.Occupied, true)]
    [InlineData(TableStatus.Occupied, TableStatus.Billed, true)]
    [InlineData(TableStatus.Billed, TableStatus.Cleaning, true)]
    [InlineData(TableStatus.Cleaning, TableStatus.Free, true)]
    [InlineData(TableStatus.Billed, TableStatus.Occupied, true)]
    [InlineData(TableStatus.Occupied, TableStatus.Free, true)]
    [InlineData(TableStatus.Free, TableStatus.Billed, false)]
    [InlineData(TableStatus.Cleaning, TableStatus.Occupied, false)]
    [InlineData(TableStatus.Occupied, TableStatus.Occupied, false)]
    public void A_table_follows_its_turn(TableStatus from, TableStatus to, bool allowed)
    {
        Assert.Equal(allowed, RestaurantTableRulesRepository.CanChange(from, to));
    }

    [Fact]
    public void A_group_is_occupied_if_anyone_is_seated()
    {
        Assert.Equal(TableStatus.Occupied, RestaurantTableRulesRepository.MergedStatus([TableStatus.Free, TableStatus.Occupied]));
        Assert.Equal(TableStatus.Free, RestaurantTableRulesRepository.MergedStatus([TableStatus.Free, TableStatus.Free]));
    }
}

/// <summary>Floors and tables through their repository (TK-145).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class RestaurantTableTests
{
    private readonly PostgresFixture _postgres;

    public RestaurantTableTests(PostgresFixture postgres) => _postgres = postgres;

    private async Task<(SalesDbContext Db, RestaurantTableRepository Tables, long[] Ids)> ArrangeAsync(int count = 4)
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        SalesDbContext db = _postgres.CreateContext(customer, org);
        RestaurantTableRepository tables = new(db);

        Assert.Equal(1, await tables.SeedFloorAsync(org, "Restaurant", default));
        long floorId = (await tables.FloorPlanAsync(false, default)).Floors.Single().FloorId;

        long[] ids = new long[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = (await tables.SaveTableAsync(null, new SaveRestaurantTableRequest { FloorId = floorId, Code = $"t{i + 1}", Seats = 4 }, default)).Value;
        }

        return (db, tables, ids);
    }

    private static async Task<RestaurantTableView> TableAsync(RestaurantTableRepository tables, long id) =>
        (await tables.FloorPlanAsync(true, default)).Floors.SelectMany(f => f.Tables).Single(t => t.RestaurantTableId == id);

    [Fact]
    public async Task Seeding_adds_one_floor_once_and_only_for_a_restaurant()
    {
        Guid org = Guid.NewGuid();
        await using SalesDbContext db = _postgres.CreateContext(Guid.NewGuid(), org);
        RestaurantTableRepository tables = new(db);

        Assert.Equal(0, await tables.SeedFloorAsync(org, "Retail", default));
        Assert.Equal(1, await tables.SeedFloorAsync(org, "Restaurant", default));
        Assert.Equal(0, await tables.SeedFloorAsync(org, "Restaurant", default));
    }

    [Fact]
    public async Task Codes_are_stored_in_capitals_and_unique()
    {
        var (db, tables, ids) = await ArrangeAsync(1);
        await using (db)
        {
            Assert.Equal("T1", (await TableAsync(tables, ids[0])).Code);
            long floorId = (await TableAsync(tables, ids[0])).FloorId;
            TableResult<long> again = await tables.SaveTableAsync(null, new SaveRestaurantTableRequest { FloorId = floorId, Code = " T1 " }, default);
            Assert.Equal("Table T1 already exists.", again.Message);
        }
    }

    [Fact]
    public async Task A_table_runs_its_turn_and_refuses_a_skipped_step()
    {
        var (db, tables, ids) = await ArrangeAsync(1);
        await using (db)
        {
            Assert.Equal(TableOutcome.Refused, (await tables.SetStatusAsync(ids[0], TableStatus.Billed, default)).Outcome);

            foreach (TableStatus next in new[] { TableStatus.Occupied, TableStatus.Billed, TableStatus.Cleaning, TableStatus.Free })
            {
                Assert.Equal(TableOutcome.Ok, (await tables.SetStatusAsync(ids[0], next, default)).Outcome);
                Assert.Equal(next, (await TableAsync(tables, ids[0])).Status);
            }
        }
    }

    [Fact]
    public async Task Moving_a_table_takes_its_status_and_its_group_with_it()
    {
        var (db, tables, ids) = await ArrangeAsync(4);
        await using (db)
        {
            await tables.SetStatusAsync(ids[0], TableStatus.Occupied, default);
            await tables.MergeAsync(ids[0], [ids[1]], default);

            Assert.Equal(TableOutcome.Ok, (await tables.MoveAsync(ids[0], ids[2], default)).Outcome);

            Assert.Equal(TableStatus.Free, (await TableAsync(tables, ids[0])).Status);
            Assert.Equal(TableStatus.Occupied, (await TableAsync(tables, ids[2])).Status);
            Assert.Equal(ids[2], (await TableAsync(tables, ids[1])).MergedIntoTableId);
            Assert.Equal(["T2"], (await TableAsync(tables, ids[2])).MergedCodes);
        }
    }

    [Fact]
    public async Task A_party_cannot_move_onto_an_occupied_table_or_from_a_member()
    {
        var (db, tables, ids) = await ArrangeAsync(3);
        await using (db)
        {
            await tables.SetStatusAsync(ids[0], TableStatus.Occupied, default);
            await tables.SetStatusAsync(ids[1], TableStatus.Occupied, default);
            Assert.Equal("Table T2 is not free.", (await tables.MoveAsync(ids[0], ids[1], default)).Message);

            await tables.SplitAsync(ids[0], default);
            await tables.MergeAsync(ids[0], [ids[2]], default);
            Assert.Contains("Move the group from table T1", (await tables.MoveAsync(ids[2], ids[1], default)).Message);
        }
    }

    [Fact]
    public async Task A_merged_group_moves_as_one_through_its_head()
    {
        var (db, tables, ids) = await ArrangeAsync(3);
        await using (db)
        {
            await tables.SetStatusAsync(ids[1], TableStatus.Occupied, default);
            Assert.Equal(TableOutcome.Ok, (await tables.MergeAsync(ids[0], [ids[1], ids[2]], default)).Outcome);

            // Anyone seated makes the whole group occupied.
            foreach (long id in ids)
            {
                Assert.Equal(TableStatus.Occupied, (await TableAsync(tables, id)).Status);
            }

            // A member's status is the head's to change.
            Assert.Contains("Change it on table T1", (await tables.SetStatusAsync(ids[1], TableStatus.Billed, default)).Message);

            await tables.SetStatusAsync(ids[0], TableStatus.Billed, default);
            Assert.All(ids, id => Assert.Equal(TableStatus.Billed, TableAsync(tables, id).Result.Status));

            // Freed, the group comes apart.
            await tables.SetStatusAsync(ids[0], TableStatus.Free, default);
            Assert.All(ids, id => Assert.Null(TableAsync(tables, id).Result.MergedIntoTableId));
        }
    }

    [Fact]
    public async Task A_table_in_one_group_cannot_join_another()
    {
        var (db, tables, ids) = await ArrangeAsync(4);
        await using (db)
        {
            await tables.MergeAsync(ids[0], [ids[1]], default);

            Assert.Contains("in another group", (await tables.MergeAsync(ids[2], [ids[1]], default)).Message);
            Assert.Contains("in another group", (await tables.MergeAsync(ids[2], [ids[0]], default)).Message);
            Assert.Contains("already part of T1's group", (await tables.MergeAsync(ids[1], [ids[3]], default)).Message);
        }
    }

    [Fact]
    public async Task Splitting_frees_the_tables_that_leave()
    {
        var (db, tables, ids) = await ArrangeAsync(3);
        await using (db)
        {
            await tables.SetStatusAsync(ids[0], TableStatus.Occupied, default);
            await tables.MergeAsync(ids[0], [ids[1], ids[2]], default);

            Assert.Equal(1, (await tables.SplitAsync(ids[2], default)).Value);
            Assert.Equal(TableStatus.Free, (await TableAsync(tables, ids[2])).Status);
            Assert.Null((await TableAsync(tables, ids[2])).MergedIntoTableId);

            Assert.Equal(1, (await tables.SplitAsync(ids[0], default)).Value);
            Assert.Equal(TableStatus.Occupied, (await TableAsync(tables, ids[0])).Status);
            Assert.Equal(TableOutcome.Refused, (await tables.SplitAsync(ids[0], default)).Outcome);
        }
    }

    [Fact]
    public async Task A_table_in_use_cannot_be_deleted_or_switched_off_and_a_floor_with_tables_cannot_be_deleted()
    {
        var (db, tables, ids) = await ArrangeAsync(1);
        await using (db)
        {
            await tables.SetStatusAsync(ids[0], TableStatus.Occupied, default);
            Assert.Equal(TableOutcome.Refused, (await tables.DeleteTableAsync(ids[0], default)).Outcome);

            long floorId = (await TableAsync(tables, ids[0])).FloorId;
            TableResult<long> off = await tables.SaveTableAsync(ids[0], new SaveRestaurantTableRequest { FloorId = floorId, Code = "T1", IsActive = false }, default);
            Assert.Equal(TableOutcome.Refused, off.Outcome);
            Assert.Equal(TableOutcome.Refused, (await tables.DeleteFloorAsync(floorId, default)).Outcome);
            Assert.True(await db.RestaurantTables.AnyAsync());
        }
    }
}
