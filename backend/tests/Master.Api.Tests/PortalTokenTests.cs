using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// A portal token names its branch's business type (TK-69, TK-136): a School
/// guardian's token says School, so the parent portal routes accept it and a
/// trading statement route refuses it; a trading branch's token reads as Retail.
/// </summary>
public sealed class PortalTokenTests
{
    private static JwtTokenRepository Tokens() => new(Microsoft.Extensions.Options.Options.Create(new JwtOptionsRepository
    {
        Issuer = "bill-book",
        Audience = "bill-book",
        SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
        RefreshTokenDays = 7,
    }), TimeProvider.System);

    private static ClaimsPrincipal Read(string token) =>
        new(new ClaimsIdentity(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims, "test"));

    private static readonly RequireBusinessTypeAttribute SchoolOnly = new(BusinessType.School);
    private static readonly RequireBusinessTypeAttribute Trading = new(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery);

    [Fact]
    public void A_school_portal_token_names_school_and_only_school_routes_accept_it()
    {
        ClaimsPrincipal guardian = Read(Tokens().CreatePortalToken(Guid.NewGuid(), Guid.NewGuid(), 42, 7, BusinessType.School).Token);

        Assert.Equal("42", guardian.FindFirst(RequirePortalAccessAttribute.ContactClaim)?.Value);
        Assert.Equal("true", guardian.FindFirst(RequirePortalAccessAttribute.AccessClaim)?.Value);
        Assert.Equal(BusinessType.School, ScopeClaims.BusinessTypeOf(guardian));
        Assert.True(SchoolOnly.Allows(ScopeClaims.BusinessTypeOf(guardian)));
        Assert.False(Trading.Allows(ScopeClaims.BusinessTypeOf(guardian)));
    }

    [Fact]
    public void A_portal_token_with_no_business_type_reads_as_retail()
    {
        ClaimsPrincipal customer = Read(Tokens().CreatePortalToken(Guid.NewGuid(), Guid.NewGuid(), 42, 7).Token);

        Assert.Null(customer.FindFirst(ScopeClaims.BusinessTypeClaim));
        Assert.Null(customer.FindFirst("app"));
        Assert.Equal(BusinessType.Retail, ScopeClaims.BusinessTypeOf(customer));
        Assert.False(SchoolOnly.Allows(ScopeClaims.BusinessTypeOf(customer)));
    }

    [Fact]
    public void A_portal_token_carries_no_staff_permissions() =>
        Assert.Empty(Read(Tokens().CreatePortalToken(Guid.NewGuid(), Guid.NewGuid(), 42, 7, BusinessType.School).Token).FindAll("permission"));

    /// <summary>
    /// A session, not a link (TK-94): an hour long, naming the grant it was
    /// issued for, so a revoked grant's sessions end within the hour and a
    /// 30-day link token, which names no grant, is refused everywhere.
    /// </summary>
    [Fact]
    public void A_portal_token_lasts_an_hour_and_names_its_grant()
    {
        (string token, DateTimeOffset expires) = Tokens().CreatePortalToken(Guid.NewGuid(), Guid.NewGuid(), 42, 7);

        JwtSecurityToken read = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("7", read.Claims.Single(c => c.Type == RequirePortalAccessAttribute.GrantClaim).Value);
        Assert.InRange(expires - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
        Assert.InRange(read.ValidTo - DateTime.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
    }
}
