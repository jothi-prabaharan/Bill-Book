using Inventory.Api.Services;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// Metal purities follow the branch's trade (D-10, TK-30): a Pharma branch is
/// seeded with none, and switching it to Jewellery seeds them once — a second
/// seed adds nothing, and switching back deletes nothing.
///
/// <b>General is seeded with them</b>, as the owner decided for the everything
/// branch (<c>Vertical</c>, master.md 5.14). The card's wording asked for the
/// opposite on General; see TK-30's outcome.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class TradeSeedTests
{
    private readonly PostgresFixture _postgres;

    public TradeSeedTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task A_pharma_branch_gets_no_purities_and_switching_to_jewellery_seeds_them_once()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var purities = new MetalPurityService(db);

        Assert.Equal(0, await purities.SeedForOrganizationAsync(orgId, "Pharma", default));
        Assert.Equal(0, await db.MetalPurities.CountAsync());

        int seeded = await purities.SeedForOrganizationAsync(orgId, "Jewellery", default);
        Assert.True(seeded > 0);
        Assert.Equal(seeded, await db.MetalPurities.CountAsync());

        // Seeding again adds nothing; going back to Pharma removes nothing.
        Assert.Equal(0, await purities.SeedForOrganizationAsync(orgId, "Jewellery", default));
        Assert.Equal(0, await purities.SeedForOrganizationAsync(orgId, "Pharma", default));
        Assert.Equal(seeded, await db.MetalPurities.CountAsync());
    }

    [SkippableFact]
    public async Task A_general_branch_is_seeded_with_purities()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);

        Assert.True(await new MetalPurityService(db).SeedForOrganizationAsync(orgId, "General", default) > 0);
    }
}
