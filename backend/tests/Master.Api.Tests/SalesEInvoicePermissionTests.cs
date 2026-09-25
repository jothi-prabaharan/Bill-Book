using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// <c>sales.einvoice</c> (TK-92): seeded, granted to the roles that hold sales,
/// and granted with fixed ids so no earlier grant was renumbered.
/// </summary>
public sealed class SalesEInvoicePermissionTests
{
    private static AdminDbContext Model() =>
        new(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql("Host=unused").Options);

    private static List<IDictionary<string, object?>> Seed<T>(AdminDbContext db) =>
        db.Model.FindEntityType(typeof(T))!.GetSeedData().ToList();

    [Fact]
    public void The_permission_is_seeded()
    {
        using AdminDbContext db = Model();

        IDictionary<string, object?> permission = Assert.Single(Seed<Permission>(db), p => (string)p["Code"]! == "sales.einvoice");
        Assert.Equal(10_003, permission["PermissionId"]);
    }

    [Fact]
    public void Owner_administrator_and_sales_hold_it_and_accountant_and_viewer_do_not()
    {
        using AdminDbContext db = Model();

        List<int> holders = [.. Seed<RolePermission>(db)
            .Where(g => (int)g["PermissionId"]! == 10_003)
            .Select(g => (int)g["RoleId"]!)
            .OrderBy(r => r)];

        Assert.Equal([1, 2, 4], holders);
    }

    [Fact]
    public void Its_grants_have_fixed_ids_outside_the_sequential_range()
    {
        using AdminDbContext db = Model();

        Assert.All(
            Seed<RolePermission>(db).Where(g => (int)g["PermissionId"]! == 10_003),
            g => Assert.Equal(900_000_000L + (100_000L * (int)g["RoleId"]!) + 10_003, (long)g["RolePermissionId"]!));
    }
}
