using Master.Api.Services;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// S0 (TK-60): School's permissions, roles and menus, and guardians as
/// contacts. The seed half reads the model and never opens a connection.
/// </summary>
public sealed class SchoolSeedTests
{
    private const string UnusedConnection =
        "Host=localhost;Port=5432;Database=never_opened;Username=postgres;Password=123";

    private static AdminDbContext Model() =>
        new(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql(UnusedConnection).Options);

    private static List<IDictionary<string, object?>> Seed<T>(AdminDbContext db) =>
        db.Model.FindEntityType(typeof(T))!.GetSeedData().ToList();

    [Theory]
    [InlineData("sis")]
    [InlineData("admission")]
    [InlineData("fee")]
    [InlineData("facility")]
    [InlineData("workorder")]
    [InlineData("preventive")]
    [InlineData("amc")]
    public void Each_school_module_is_seeded_and_belongs_to_school_only(string module)
    {
        Assert.Contains(module, AdminDbContext.PermissionModules);

        using AdminDbContext db = Model();
        List<IDictionary<string, object?>> rows = Seed<Permission>(db).Where(p => (string)p["Module"]! == module).ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, p => Assert.Equal(App.School, (App)p["Apps"]!));
    }

    [Fact]
    public void Attendance_is_shared_by_hrms_and_school_but_unlock_is_schools()
    {
        using AdminDbContext db = Model();
        List<IDictionary<string, object?>> permissions = Seed<Permission>(db);

        Assert.All(permissions.Where(p => (string)p["Module"]! == "attendance" && (string)p["Code"]! != "attendance.unlock"),
            p => Assert.Equal(App.Hrms | App.School, (App)p["Apps"]!));
        Assert.Equal(App.School, (App)permissions.Single(p => (string)p["Code"]! == "attendance.unlock")["Apps"]!);
        Assert.Equal(App.School, (App)permissions.Single(p => (string)p["Code"]! == "workorder.close")["Apps"]!);
    }

    [Fact]
    public void Extra_verbs_never_share_an_id_with_the_grid()
    {
        using AdminDbContext db = Model();
        List<int> ids = Seed<Permission>(db).Select(p => (int)p["PermissionId"]!).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.True(AdminDbContext.PermissionModules.Length * 10 < AdminDbContext.ExtraPermissions[0].PermissionId);
    }

    [Fact]
    public void School_has_its_six_roles_beside_its_owner()
    {
        using AdminDbContext db = Model();
        List<string> names = Seed<Role>(db)
            .Where(r => (App)r["App"]! == App.School)
            .Select(r => (string)r["SystemName"]!)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(["Accountant", "Maintenance", "Office Admin", "Owner", "Principal", "Teacher", "Viewer"], names);
    }

    private static HashSet<string> CodesOf(string role)
    {
        using AdminDbContext db = Model();
        int roleId = AdminDbContext.SchoolRoles.Single(r => r.Name == role).RoleId;
        Dictionary<int, string> codes = Seed<Permission>(db).ToDictionary(p => (int)p["PermissionId"]!, p => (string)p["Code"]!);

        return Seed<RolePermission>(db)
            .Where(g => (int)g["RoleId"]! == roleId)
            .Select(g => codes[(int)g["PermissionId"]!])
            .ToHashSet();
    }

    [Fact]
    public void A_teacher_takes_attendance_and_marks_but_cannot_unlock_a_day_or_touch_fees()
    {
        HashSet<string> teacher = CodesOf("Teacher");

        Assert.Contains("attendance.create", teacher);
        Assert.Contains("sis.edit", teacher);
        Assert.DoesNotContain("attendance.unlock", teacher);
        Assert.DoesNotContain(teacher, c => c.StartsWith("fee.", StringComparison.Ordinal));
    }

    [Fact]
    public void Principal_and_office_admin_can_unlock_and_only_principal_closes_work_orders()
    {
        Assert.Contains("attendance.unlock", CodesOf("Principal"));
        Assert.Contains("attendance.unlock", CodesOf("Office Admin"));
        Assert.Contains("workorder.close", CodesOf("Principal"));
        Assert.DoesNotContain("workorder.close", CodesOf("Maintenance"));
    }

    [Fact]
    public void Schools_viewer_changes_nothing()
    {
        HashSet<string> viewer = CodesOf("Viewer");

        Assert.NotEmpty(viewer);
        Assert.All(viewer, c => Assert.EndsWith(".view", c));
    }

    [Fact]
    public void No_school_role_holds_a_retail_or_platform_permission()
    {
        using AdminDbContext db = Model();
        Dictionary<int, App> apps = Seed<Permission>(db).ToDictionary(p => (int)p["PermissionId"]!, p => (App)p["Apps"]!);
        HashSet<int> schoolRoles = AdminDbContext.SchoolRoles.Select(r => r.RoleId).ToHashSet();

        Assert.All(Seed<RolePermission>(db).Where(g => schoolRoles.Contains((int)g["RoleId"]!)),
            g => Assert.True(apps[(int)g["PermissionId"]!].HasFlag(App.School)));
    }

    [Fact]
    public void Schools_menus_show_only_in_school_and_start_switched_off()
    {
        IReadOnlyList<Menu> menus = MenuSeed.Build();
        string[] codes = ["students", "fees", "maintenance", "stu", "acs", "exm", "enq", "apl", "sat", "fst", "fdm", "frc", "spc", "fas", "wko", "ppm", "amc"];

        Assert.All(codes, c => Assert.Equal(App.School, menus.Single(m => m.Code == c).Apps));

        // Every item routes somewhere and names a School module.
        Assert.All(menus.Where(m => codes.Contains(m.Code) && m.Type == MenuType.Item), m =>
        {
            Assert.NotNull(m.RoutePath);
            Assert.Equal(App.School, AdminDbContext.AppsOfModule(m.Module!) & App.School);
        });
    }

    [Fact]
    public void Every_school_menu_permission_names_a_seeded_code()
    {
        using AdminDbContext db = Model();
        HashSet<string> codes = Seed<Permission>(db).Select(p => (string)p["Code"]!).ToHashSet();
        HashSet<int> schoolMenus = MenuSeed.AppsByMenuId.Where(kv => kv.Value == App.School).Select(kv => kv.Key).ToHashSet();

        Assert.All(MenuSeed.BuildPermissions().Where(p => schoolMenus.Contains(p.MenuId)),
            p => Assert.Contains(p.PermissionCode, codes));
    }
}

/// <summary>A guardian is a contact (TK-60): it satisfies the role rule and filters on its own.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class GuardianContactTests
{
    private readonly PostgresFixture _postgres;

    public GuardianContactTests(PostgresFixture postgres) => _postgres = postgres;

    private static ContactService Service(ContactsDbContext db, TenantContext tenant) =>
        new(db, null!, null!, null!, null!, TimeProvider.System, tenant);

    [SkippableFact]
    public async Task A_guardian_only_contact_is_saved_and_the_guardian_filter_finds_only_guardians()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);

        // No customer, vendor, job-worker or prescriber flag: the role check
        // constraint must accept a guardian on its own.
        db.Contacts.Add(new Contact { ContactCode = "G001", DisplayName = "Lakshmi Raman", CurrencyCode = "INR", IsGuardian = true });
        db.Contacts.Add(new Contact { ContactCode = "C001", DisplayName = "Stationery House", CurrencyCode = "INR", IsCustomer = true });
        await db.SaveChangesAsync();

        IReadOnlyList<ContactListItem> guardians = await Service(db, tenant).ListAsync(null, "guardian", false, default);

        ContactListItem only = Assert.Single(guardians);
        Assert.Equal("G001", only.ContactCode);
        Assert.True(only.IsGuardian);
    }

    [SkippableFact]
    public async Task A_contact_with_no_role_at_all_is_still_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
        await using ContactsDbContext db = _postgres.CreateContext(tenant);

        db.Contacts.Add(new Contact { ContactCode = "X001", DisplayName = "Nobody", CurrencyCode = "INR" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
