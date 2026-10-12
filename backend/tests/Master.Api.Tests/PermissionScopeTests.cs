using Master.Repository.Repositories;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shared.Kernel.Apps;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Roles span every trade and add-on (D-36), and the roles screen's matrix
/// shows the permissions the current branch's business type and add-ons allow
/// (D-50, TK-136), by the same rule the menu uses. Replaces the per-app grant
/// rule of TK-42, retired with the App enum.
/// </summary>
public sealed class PermissionScopeTests
{
    private const string UnusedConnection =
        "Host=localhost;Port=5432;Database=never_opened;Username=postgres;Password=123";

    private static AdminDbContext Model() =>
        new(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql(UnusedConnection).Options);

    private static List<IDictionary<string, object?>> Seed<T>(AdminDbContext db) =>
        db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(T))!.GetSeedData().ToList();

    [Theory]
    // Shared permissions (settings) reach every branch, and a customer with no trade.
    [InlineData(null, AddOn.None, BusinessType.Retail, AddOn.None, true)]
    [InlineData(null, AddOn.None, null, AddOn.Payroll, true)]
    // Trading permissions reach every trading branch, Service included, and not a school.
    [InlineData(BusinessType.Retail, AddOn.None, BusinessType.Retail, AddOn.None, true)]
    [InlineData(BusinessType.Retail, AddOn.None, BusinessType.Service, AddOn.None, true)]
    [InlineData(BusinessType.Retail, AddOn.None, BusinessType.Restaurant, AddOn.None, true)]
    [InlineData(BusinessType.Retail, AddOn.None, BusinessType.School, AddOn.None, false)]
    [InlineData(BusinessType.Retail, AddOn.None, null, AddOn.Hrms, false)]
    // School's own, and an add-on's own, reach only what has them.
    [InlineData(BusinessType.School, AddOn.None, BusinessType.School, AddOn.None, true)]
    [InlineData(BusinessType.School, AddOn.None, BusinessType.Retail, AddOn.None, false)]
    [InlineData(null, AddOn.Payroll, BusinessType.Retail, AddOn.None, false)]
    [InlineData(null, AddOn.Payroll, BusinessType.Retail, AddOn.Payroll, true)]
    [InlineData(BusinessType.Restaurant, AddOn.None, BusinessType.Retail, AddOn.None, false)]
    public void A_permission_is_offered_to_a_branch_by_the_menus_rule(
        BusinessType? permissionType, AddOn permissionAddOns, BusinessType? branchType, AddOn branchAddOns, bool offered)
    {
        Assert.Equal(offered, RoleRepository.InScope(permissionType, permissionAddOns, branchType, branchAddOns));
        Assert.Equal(offered, MenuRepository.MenuMatches(branchType, branchAddOns, permissionType, permissionAddOns));
    }

    /// <summary>
    /// Projects (TK-104): Owner, Administrator and Accountant run them, Sales and
    /// Viewer only read them, and the grants take fixed ids so no earlier grant
    /// was renumbered.
    /// </summary>
    [Fact]
    public void Projects_are_run_by_the_accounts_side_and_read_by_sales()
    {
        using AdminDbContext db = Model();
        Dictionary<int, string> codes = Seed<Permission>(db).ToDictionary(p => (int)p["PermissionId"]!, p => (string)p["Code"]!);
        List<IDictionary<string, object?>> grants = Seed<RolePermission>(db)
            .Where(g => codes[(int)g["PermissionId"]!].StartsWith("projects.", StringComparison.Ordinal))
            .ToList();

        HashSet<string> Held(int roleId) =>
            [.. grants.Where(g => (int)g["RoleId"]! == roleId).Select(g => codes[(int)g["PermissionId"]!])];

        Assert.Contains("projects.edit", Held(1));
        Assert.Contains("projects.edit", Held(2));
        Assert.Contains("projects.edit", Held(3));
        Assert.Equal(["projects.view"], Held(4));
        Assert.Equal(["projects.view"], Held(5));
        Assert.All(grants, g => Assert.True((long)g["RolePermissionId"]! >= 910_000_000L));
        Assert.Contains("projects", AdminDbContext.PermissionModules);
    }

    [Fact]
    public void Every_original_permission_is_retails_or_everyones()
    {
        using AdminDbContext db = Model();

        // Rows seeded before other apps' modules arrived (menus to 1106, the
        // first twelve modules). Later rows belong to HRMS, Payroll and School.
        string[] original =
        [
            "dashboard", "contacts", "crm", "inventory", "sales", "purchase",
            "accounting", "banking", "reports", "settings", "support", "platform",
        ];
        Assert.All(Seed<Permission>(db).Where(p => original.Contains((string)p["Module"]!)),
            p => Assert.True(p["BusinessType"] is null || (BusinessType)p["BusinessType"]! == BusinessType.Retail, $"Permission {p["Code"]}"));
    }

    /// <summary>The employee master is HRMS's, Payroll's and School's; lifecycle is HRMS's (TK-48).</summary>
    [Fact]
    public void The_employee_master_belongs_to_the_apps_that_employ_people()
    {
        using AdminDbContext db = Model();
        List<IDictionary<string, object?>> permissions = Seed<Permission>(db);

        Assert.All(permissions.Where(p => (string)p["Module"]! == "employee"),
            p => Assert.Equal(AddOn.Hrms | AddOn.Payroll, (AddOn)p["AddOns"]!));
        Assert.All(permissions.Where(p => (string)p["Module"]! == "hrm"),
            p => Assert.Equal(AddOn.Hrms, (AddOn)p["AddOns"]!));

        IReadOnlyList<Menu> menus = MenuSeed.Build();
        Assert.Equal(AddOn.Hrms | AddOn.Payroll, menus.Single(m => m.Code == "emp").AddOns);
        Assert.Equal(AddOn.Hrms, menus.Single(m => m.Code == "ann").AddOns);
    }

    [Fact]
    public void The_shared_settings_screens_belong_to_no_trade_and_the_trading_screens_to_retail()
    {
        IReadOnlyList<Menu> menus = MenuSeed.Build();

        Assert.Null(menus.Single(m => m.Code == "usr").BusinessType);
        Assert.Equal(AddOn.None, menus.Single(m => m.Code == "usr").AddOns);
        Assert.Null(menus.Single(m => m.Code == "rol").BusinessType);
        Assert.Null(menus.Single(m => m.Code == "brn").BusinessType);
        Assert.Null(menus.Single(m => m.Code == "settings" && m.ParentId is null).BusinessType);
        Assert.Equal(BusinessType.Retail, menus.Single(m => m.Code == "inv").BusinessType);
        Assert.Equal(BusinessType.Retail, menus.Single(m => m.Code == "tax").BusinessType);

        // Projects sit under Accounting, a trading screen (TK-104).
        Assert.Equal(BusinessType.Retail, menus.Single(m => m.Code == "prj").BusinessType);

        // Every branch configures its chains on one page (TK-103).
        Assert.Null(menus.Single(m => m.Code == "apw").BusinessType);
        Assert.Null(menus.Single(m => m.Code == "inbox").BusinessType);
    }
}

/// <summary>The branch-scoped matrix (D-50) against a real database.</summary>
[Collection(nameof(AdminCollection))]
public sealed class RoleMatrixScopeTests
{
    private readonly AdminFixture _admin;

    public RoleMatrixScopeTests(AdminFixture admin) => _admin = admin;

    [SkippableFact]
    public async Task A_payroll_only_customers_matrix_offers_settings_and_payroll_but_no_sales()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        IReadOnlyList<PermissionGroup> matrix = await new RoleRepository(db).PermissionMatrixAsync(false, null, AddOn.Payroll, default);

        Assert.Contains(matrix, g => g.Module == "settings");
        Assert.Contains(matrix, g => g.Module == "payroll");
        Assert.DoesNotContain(matrix, g => g.Module == "sales");
        Assert.DoesNotContain(matrix, g => g.Module == "platform");
    }

    [SkippableFact]
    public async Task A_retail_branchs_matrix_offers_sales_but_no_school_or_payroll()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        IReadOnlyList<PermissionGroup> matrix = await new RoleRepository(db).PermissionMatrixAsync(false, BusinessType.Retail, AddOn.None, default);

        Assert.Contains(matrix, g => g.Module == "sales");
        Assert.Contains(matrix, g => g.Module == "settings");
        Assert.DoesNotContain(matrix, g => g.Module == "sis");
        Assert.DoesNotContain(matrix, g => g.Module == "payroll");
    }

    [SkippableFact]
    public async Task Any_permission_can_be_granted_now_that_roles_span_trades()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        int payroll = await db.Permissions.Where(p => p.Module == "payroll").Select(p => p.PermissionId).FirstAsync();
        int sales = await db.Permissions.Where(p => p.Code == "sales.view").Select(p => p.PermissionId).SingleAsync();

        (SaveRoleResult result, int roleId) = await new RoleRepository(db).CreateAsync(
            Guid.NewGuid(),
            new SaveRoleRequest { DisplayName = $"Mixed {Guid.NewGuid():N}"[..20], PermissionIds = [payroll, sales] },
            default);

        Assert.Equal(SaveRoleResult.Ok, result);
        Assert.True(roleId > 0);
    }

    [SkippableFact]
    public async Task Deleting_a_role_soft_deletes_and_removes_from_list_and_can_be_recreated()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        var repo = new RoleRepository(db);
        Guid customerId = Guid.NewGuid();
        string roleName = $"CustomRole_{Guid.NewGuid():N}"[..20];

        int sales = await db.Permissions.Where(p => p.Code == "sales.view").Select(p => p.PermissionId).SingleAsync();

        (SaveRoleResult createRes, int roleId) = await repo.CreateAsync(
            customerId,
            new SaveRoleRequest { DisplayName = roleName, PermissionIds = [sales] },
            default);
        Assert.Equal(SaveRoleResult.Ok, createRes);

        var listBefore = await repo.ListAsync(customerId, null, default);
        Assert.Contains(listBefore, r => r.RoleId == roleId);

        DeleteRoleResult deleteRes = await repo.DeleteAsync(customerId, roleId, default);
        Assert.Equal(DeleteRoleResult.Ok, deleteRes);

        var listAfter = await repo.ListAsync(customerId, null, default);
        Assert.DoesNotContain(listAfter, r => r.RoleId == roleId);

        // Re-creating with the same name reactivates the role cleanly
        (SaveRoleResult recreateRes, int newRoleId) = await repo.CreateAsync(
            customerId,
            new SaveRoleRequest { DisplayName = roleName, PermissionIds = [sales] },
            default);
        Assert.Equal(SaveRoleResult.Ok, recreateRes);
        Assert.Equal(roleId, newRoleId);

        var listRecreated = await repo.ListAsync(customerId, null, default);
        Assert.Contains(listRecreated, r => r.RoleId == roleId);
    }
}
