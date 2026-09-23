using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The model and its migrations must agree, or Master does not start.
///
/// <b>This is what broke on 23 September 2026.</b> The menu seed was renumbered
/// after the admin migration's snapshot was generated, so <c>MigrateAsync</c>
/// threw <c>PendingModelChangesWarning</c> and every host refused to start on a
/// fresh database. Nothing caught it: every other suite migrates a database and
/// then works in it, and a developer database that was migrated before the seed
/// changed never runs that check again.
///
/// <b>No database is needed.</b> <c>HasPendingModelChanges</c> compares the
/// model with the snapshot in the migrations assembly and never opens the
/// connection, so these tests cannot skip. Run them after touching any
/// <c>HasData</c>; the fix for a failure is to regenerate the migration, not to
/// add one on top — see TK-01 in <c>docs/TASKS.md</c>.
/// </summary>
public sealed class MigrationModelTests
{
    private const string UnusedConnection =
        "Host=localhost;Port=5432;Database=never_opened;Username=postgres;Password=123";

    [Fact]
    public void AdminDbContext_has_no_changes_outside_its_migrations()
    {
        using var db = new AdminDbContext(
            new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql(UnusedConnection).Options);

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "AdminDbContext has drifted from its migrations. Regenerate the admin migration.");
    }

    [Fact]
    public void ContactsDbContext_has_no_changes_outside_its_migrations()
    {
        using var db = new ContactsDbContext(
            new DbContextOptionsBuilder<ContactsDbContext>().UseNpgsql(UnusedConnection).Options,
            new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" });

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "ContactsDbContext has drifted from its migrations. Regenerate the contacts migration.");
    }

    /// <summary>
    /// <c>IX_MenuPermissions_MenuId_PermissionCode</c> is unique. <c>HasData</c>
    /// checks primary keys when the model is built but not other unique indexes,
    /// so a duplicate pair here fails only when the migration inserts it.
    /// </summary>
    [Fact]
    public void Menu_permission_seed_has_one_row_per_menu_and_permission()
    {
        var duplicates = MenuSeed.BuildPermissions()
            .GroupBy(p => (p.MenuId, p.PermissionCode))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.MenuId}/{g.Key.PermissionCode}")
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Menu_permission_seed_names_only_seeded_menus()
    {
        var menuIds = MenuSeed.Build().Select(m => m.MenuId).ToHashSet();

        var orphans = MenuSeed.BuildPermissions()
            .Where(p => !menuIds.Contains(p.MenuId))
            .Select(p => p.MenuPermissionId)
            .ToList();

        Assert.Empty(orphans);
    }

    [Fact]
    public void Menu_seed_parents_are_seeded_menus()
    {
        var menus = MenuSeed.Build();
        var menuIds = menus.Select(m => m.MenuId).ToHashSet();

        var orphans = menus
            .Where(m => m.ParentId is int parent && !menuIds.Contains(parent))
            .Select(m => m.MenuId)
            .ToList();

        Assert.Empty(orphans);
    }
}
