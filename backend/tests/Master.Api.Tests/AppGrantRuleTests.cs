using Master.Api.Services;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// H0.1 (TK-42): a role belongs to one app, a permission or a menu to one or
/// more, and a role may hold only permissions whose apps include its own.
/// </summary>
public sealed class AppGrantRuleTests
{
    private const string UnusedConnection =
        "Host=localhost;Port=5432;Database=never_opened;Username=postgres;Password=123";

    private static AdminDbContext Model() =>
        new(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql(UnusedConnection).Options);

    private static List<IDictionary<string, object?>> Seed<T>(AdminDbContext db) =>
        db.Model.FindEntityType(typeof(T))!.GetSeedData().ToList();

    [Theory]
    [InlineData(App.RetailErp, App.RetailErp, true)]
    [InlineData(App.Payroll, App.All, true)]
    [InlineData(App.RetailErp, App.Payroll, false)]
    [InlineData(App.Hrms, App.Hrms | App.Payroll, true)]
    [InlineData(App.School, App.Hrms | App.Payroll, false)]
    [InlineData(App.All, App.All, false)]
    [InlineData(App.None, App.All, false)]
    public void The_grant_rule(App roleApp, App permissionApps, bool allowed) =>
        Assert.Equal(allowed, AppRules.MayGrant(roleApp, permissionApps));

    [Theory]
    [InlineData("Payroll", true, App.Payroll)]
    [InlineData("hrms", true, App.Hrms)]
    [InlineData("All", false, App.None)]
    [InlineData("4", false, App.None)]
    [InlineData("", false, App.None)]
    [InlineData("Accounting", false, App.None)]
    public void Only_a_single_app_parses(string value, bool parses, App expected)
    {
        Assert.Equal(parses, AppRules.TryParseSingle(value, out App app));
        if (parses)
        {
            Assert.Equal(expected, app);
        }
    }

    /// <summary>Every grant the seed makes obeys the rule, so no migration writes a grant the service would refuse.</summary>
    [Fact]
    public void Every_seeded_grant_obeys_the_grant_rule()
    {
        using AdminDbContext db = Model();
        Dictionary<int, App> roleApps = Seed<Role>(db).ToDictionary(r => (int)r["RoleId"]!, r => (App)r["App"]!);
        Dictionary<int, App> permissionApps = Seed<Permission>(db).ToDictionary(p => (int)p["PermissionId"]!, p => (App)p["Apps"]!);

        List<IDictionary<string, object?>> grants = Seed<RolePermission>(db);

        Assert.NotEmpty(grants);
        Assert.All(grants, g =>
            Assert.True(
                AppRules.MayGrant(roleApps[(int)g["RoleId"]!], permissionApps[(int)g["PermissionId"]!]),
                $"Grant {g["RolePermissionId"]} breaks the grant rule."));
    }

    [Fact]
    public void Every_seeded_role_names_one_app_and_settings_belong_to_every_app()
    {
        using AdminDbContext db = Model();

        // The five original system roles are RetailErp's; each other app has
        // one seeded Owner (TK-45). Every role names exactly one app.
        List<IDictionary<string, object?>> roles = Seed<Role>(db);
        Assert.All(roles, r => Assert.True(AppRules.IsSingle((App)r["App"]!)));
        Assert.All(roles.Where(r => (int)r["RoleId"]! <= 5), r => Assert.Equal(App.RetailErp, (App)r["App"]!));
        // School also has its Principal, Office Admin, Accountant, Teacher,
        // Maintenance and Viewer (TK-60).
        Assert.Equal(
            [.. Enumerable.Repeat(App.School, 7), App.Hrms, App.Payroll],
            roles.Where(r => (int)r["RoleId"]! > 5).Select(r => (App)r["App"]!).OrderBy(a => a));

        List<IDictionary<string, object?>> permissions = Seed<Permission>(db);
        Assert.All(permissions.Where(p => (string)p["Module"]! == "settings"), p => Assert.Equal(App.All, (App)p["Apps"]!));
        Assert.All(permissions.Where(p => (string)p["Module"]! == "sales"), p => Assert.Equal(App.RetailErp, (App)p["Apps"]!));
    }

    /// <summary>
    /// "apps/web is unchanged for every existing user": every menu row and every
    /// permission still includes RetailErp, so nothing a RetailErp user saw is hidden.
    /// </summary>
    [Fact]
    public void Nothing_a_retail_user_had_is_taken_away()
    {
        using AdminDbContext db = Model();

        // Rows seeded before other apps' modules arrived (menus to 1106, the
        // first twelve modules). Later rows belong to HRMS, Payroll and School.
        string[] original =
        [
            "dashboard", "contacts", "crm", "inventory", "sales", "purchase",
            "accounting", "banking", "reports", "settings", "support", "platform",
        ];
        Assert.All(Seed<Menu>(db).Where(m => (int)m["MenuId"]! <= 1106 && !MenuSeed.AppsByMenuId.ContainsKey((int)m["MenuId"]!)),
            m => Assert.True(((App)m["Apps"]!).HasFlag(App.RetailErp), $"Menu {m["Code"]}"));
        Assert.All(Seed<Permission>(db).Where(p => original.Contains((string)p["Module"]!)),
            p => Assert.True(((App)p["Apps"]!).HasFlag(App.RetailErp), $"Permission {p["Code"]}"));
    }

    /// <summary>The employee master is HRMS's, Payroll's and School's; lifecycle is HRMS's (TK-48).</summary>
    [Fact]
    public void The_employee_master_belongs_to_the_apps_that_employ_people()
    {
        using AdminDbContext db = Model();
        List<IDictionary<string, object?>> permissions = Seed<Permission>(db);

        Assert.All(permissions.Where(p => (string)p["Module"]! == "employee"),
            p => Assert.Equal(App.Hrms | App.Payroll | App.School, (App)p["Apps"]!));
        Assert.All(permissions.Where(p => (string)p["Module"]! == "hrm"),
            p => Assert.Equal(App.Hrms, (App)p["Apps"]!));

        IReadOnlyList<Menu> menus = MenuSeed.Build();
        Assert.Equal(App.Hrms | App.Payroll | App.School, menus.Single(m => m.Code == "emp").Apps);
        Assert.Equal(App.Hrms, menus.Single(m => m.Code == "ann").Apps);
    }

    [Fact]
    public void The_shared_settings_screens_show_in_every_app_and_retail_screens_only_in_retail()
    {
        IReadOnlyList<Menu> menus = MenuSeed.Build();

        Assert.Equal(App.All, menus.Single(m => m.Code == "usr").Apps);
        Assert.Equal(App.All, menus.Single(m => m.Code == "rol").Apps);
        Assert.Equal(App.All, menus.Single(m => m.Code == "brn").Apps);
        Assert.Equal(App.All, menus.Single(m => m.Code == "settings" && m.ParentId is null).Apps);
        Assert.Equal(App.RetailErp, menus.Single(m => m.Code == "inv").Apps);
        Assert.Equal(App.RetailErp, menus.Single(m => m.Code == "tax").Apps);

        // Every app configures its chains on one page; the inbox is RetailErp's documents (TK-103).
        Assert.Equal(App.All, menus.Single(m => m.Code == "apw").Apps);
        Assert.Equal(App.RetailErp, menus.Single(m => m.Code == "inbox").Apps);
    }
}

/// <summary>The grant rule enforced by <see cref="RoleService"/> against a real database.</summary>
[Collection(nameof(AdminCollection))]
public sealed class RoleServiceAppTests
{
    private readonly AdminFixture _admin;

    public RoleServiceAppTests(AdminFixture admin) => _admin = admin;

    private static async Task<int> PayrollOnlyPermissionAsync(AdminDbContext db)
    {
        var permission = new Permission
        {
            Code = $"pay{Guid.NewGuid():N}"[..20] + ".view",
            Module = "payrolltest",
            Apps = App.Payroll,
        };
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();
        return permission.PermissionId;
    }

    private static async Task<int> PermissionIdAsync(AdminDbContext db, string code) =>
        await db.Permissions.Where(p => p.Code == code).Select(p => p.PermissionId).SingleAsync();

    [SkippableFact]
    public async Task Granting_a_payroll_only_permission_to_a_retail_role_is_refused()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        RoleService roles = new(db);
        Guid customer = Guid.NewGuid();
        int payrollOnly = await PayrollOnlyPermissionAsync(db);

        (SaveRoleResult created, int roleId) = await roles.CreateAsync(customer, new SaveRoleRequest
        {
            DisplayName = "Shop clerk",
            App = "RetailErp",
            PermissionIds = [payrollOnly],
        }, default);

        Assert.Equal(SaveRoleResult.PermissionNotInApp, created);
        Assert.Equal(0, roleId);
        Assert.False(await db.Roles.AnyAsync(r => r.CustomerId == customer));
    }

    [SkippableFact]
    public async Task Editing_a_retail_role_to_add_a_payroll_only_permission_is_refused_and_changes_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        RoleService roles = new(db);
        Guid customer = Guid.NewGuid();
        int salesView = await PermissionIdAsync(db, "sales.view");
        int payrollOnly = await PayrollOnlyPermissionAsync(db);

        (_, int roleId) = await roles.CreateAsync(customer, new SaveRoleRequest
        {
            DisplayName = "Counter",
            PermissionIds = [salesView],
        }, default);

        SaveRoleResult updated = await roles.UpdateAsync(customer, roleId, new SaveRoleRequest
        {
            DisplayName = "Counter",
            PermissionIds = [salesView, payrollOnly],
        }, default);

        Assert.Equal(SaveRoleResult.PermissionNotInApp, updated);
        Assert.Equal([salesView], await db.RolePermissions.Where(p => p.RoleId == roleId).Select(p => p.PermissionId).ToListAsync());
    }

    [SkippableFact]
    public async Task A_payroll_role_can_be_granted_the_shared_settings_permissions()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        RoleService roles = new(db);
        Guid customer = Guid.NewGuid();
        int settingsView = await PermissionIdAsync(db, "settings.view");
        int payrollOnly = await PayrollOnlyPermissionAsync(db);

        (SaveRoleResult created, int roleId) = await roles.CreateAsync(customer, new SaveRoleRequest
        {
            DisplayName = "Payroll admin",
            App = "Payroll",
            PermissionIds = [settingsView, payrollOnly],
        }, default);

        Assert.Equal(SaveRoleResult.Ok, created);
        Role role = await db.Roles.SingleAsync(r => r.RoleId == roleId);
        Assert.Equal(App.Payroll, role.App);
        Assert.Equal(2, await db.RolePermissions.CountAsync(p => p.RoleId == roleId));
    }

    [SkippableFact]
    public async Task A_role_must_name_one_app()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (SaveRoleResult created, _) = await new RoleService(db).CreateAsync(Guid.NewGuid(), new SaveRoleRequest
        {
            DisplayName = "Everything",
            App = "All",
        }, default);

        Assert.Equal(SaveRoleResult.InvalidApp, created);
    }

    [SkippableFact]
    public async Task The_matrix_for_an_app_offers_only_what_its_roles_may_hold()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        IReadOnlyList<PermissionGroup> payroll = await new RoleService(db).PermissionMatrixAsync(false, default, App.Payroll);

        Assert.Contains(payroll, g => g.Module == "settings");
        Assert.DoesNotContain(payroll, g => g.Module == "sales");
    }
}
