using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;
using Xunit;

namespace Master.Api.Tests;

/// <summary>A branch's staff and their names for other services (TK-146).</summary>
[Collection(nameof(AdminCollection))]
public sealed class BranchStaffTests
{
    private readonly AdminFixture _admin;

    public BranchStaffTests(AdminFixture admin) => _admin = admin;

    private static async Task<Guid> UserAsync(AdminDbContext db, string name, Guid? orgId, bool active = true, bool roleActive = true)
    {
        Guid userId = Guid.NewGuid();
        db.Users.Add(new User { UserId = userId, Email = $"{userId:N}@example.com", DisplayName = name, PasswordHash = "x", IsActive = active, EmailConfirmed = true });
        await db.SaveChangesAsync();

        if (orgId is Guid org)
        {
            Role role = await db.Roles.FirstAsync(r => r.IsSystemRole && r.SystemName == "Sales");
            db.UserOrganizationRoles.Add(new UserOrganizationRole { UserId = userId, OrgId = org, RoleId = role.RoleId, IsActive = roleActive });
            await db.SaveChangesAsync();
        }

        return userId;
    }

    private static async Task<Guid> BranchAsync(AdminDbContext db)
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = "Test Restaurant",
            BillingEmail = $"billing-{suffix}@example.com",
            Status = TenantStatus.Active,
            PlanTier = PlanTier.Standard,
            DatabaseName = "IN000001",
        });
        db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = $"R{suffix}",
            Name = "Restaurant",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
        return orgId;
    }

    [SkippableFact]
    public async Task Staff_are_the_active_users_with_an_active_role_in_the_branch()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);
        await using AdminDbContext db = _admin.CreateContext();
        Guid branch = await BranchAsync(db);
        Guid other = await BranchAsync(db);
        Guid asha = await UserAsync(db, "Asha", branch);
        await UserAsync(db, "Former", branch, active: false);
        await UserAsync(db, "Suspended", branch, roleActive: false);
        await UserAsync(db, "Elsewhere", other);

        List<UserRef> staff = await new BranchStaffRepository(db).StaffAsync(branch, default);

        Assert.Equal([asha], staff.Select(s => s.Id));
    }

    [SkippableFact]
    public async Task Names_resolve_only_for_the_branch_and_keep_a_former_waiter()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);
        await using AdminDbContext db = _admin.CreateContext();
        Guid branch = await BranchAsync(db);
        Guid former = await UserAsync(db, "Former", branch, active: false);
        Guid elsewhere = await UserAsync(db, "Elsewhere", await BranchAsync(db));

        List<UserRef> names = await new BranchStaffRepository(db).NamesAsync(branch, [former, elsewhere, Guid.NewGuid()], default);

        UserRef only = Assert.Single(names);
        Assert.Equal("Former", only.Name);
    }
}
