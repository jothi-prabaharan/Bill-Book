using Master.Api.Services;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The startup backfill turns a stored '' phone into NULL and leaves a real
/// number alone (D-04, TK-21). Run twice, the second pass finds nothing.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class BlankPhoneBackfillTests
{
    private readonly AdminFixture _admin;

    public BlankPhoneBackfillTests(AdminFixture admin) => _admin = admin;

    [SkippableFact]
    public async Task A_blank_user_mobile_becomes_null_and_a_real_one_is_kept()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        Guid blank = Guid.NewGuid();
        Guid real = Guid.NewGuid();

        db.Users.AddRange(
            new User { UserId = blank, Email = $"{blank:N}@example.test", DisplayName = "Blank", PasswordHash = "x", IsActive = true, MobileNumber = "" },
            new User { UserId = real, Email = $"{real:N}@example.test", DisplayName = "Real", PasswordHash = "x", IsActive = true, MobileNumber = "9876543210" });
        await db.SaveChangesAsync();

        Assert.True(await BlankPhoneBackfill.RunAdminAsync(db, default) >= 1);
        Assert.Equal(0, await BlankPhoneBackfill.RunAdminAsync(db, default));

        Assert.Null((await db.Users.AsNoTracking().SingleAsync(u => u.UserId == blank)).MobileNumber);
        Assert.Equal("9876543210", (await db.Users.AsNoTracking().SingleAsync(u => u.UserId == real)).MobileNumber);
    }
}
