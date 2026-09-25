using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Master.Api.Services;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// A portal link opens the portal of the app it was made from (S9, TK-69): a
/// School guardian's token names School, so the parent portal accepts it and
/// RetailErp's statement route refuses it; a RetailErp token is unchanged.
/// </summary>
public sealed class PortalTokenTests
{
    private static JwtTokenService Tokens() => new(Microsoft.Extensions.Options.Options.Create(new JwtOptions
    {
        Issuer = "bill-book",
        Audience = "bill-book",
        SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
        RefreshTokenDays = 7,
    }), TimeProvider.System);

    private static ClaimsPrincipal Read(string token) =>
        new(new ClaimsIdentity(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims, "test"));

    [Fact]
    public void A_school_portal_token_names_school_and_only_school_routes_accept_it()
    {
        ClaimsPrincipal guardian = Read(Tokens().CreatePortalToken(Guid.NewGuid(), Guid.NewGuid(), 42, App.School));

        Assert.Equal("42", guardian.FindFirst(RequirePortalAccessAttribute.ContactClaim)?.Value);
        Assert.Equal("true", guardian.FindFirst(RequirePortalAccessAttribute.AccessClaim)?.Value);
        Assert.Equal(App.School, RequireAppAttribute.AppOf(guardian));
        Assert.True(RequireAppAttribute.Allows(App.School, RequireAppAttribute.AppOf(guardian)));
        Assert.False(RequireAppAttribute.Allows(App.RetailErp, RequireAppAttribute.AppOf(guardian)));
    }

    [Fact]
    public void A_retail_portal_token_keeps_its_shape_and_reads_as_retail()
    {
        ClaimsPrincipal customer = Read(Tokens().CreatePortalToken(Guid.NewGuid(), Guid.NewGuid(), 42));

        Assert.Null(customer.FindFirst(RequireAppAttribute.ClaimType));
        Assert.Equal(App.RetailErp, RequireAppAttribute.AppOf(customer));
        Assert.False(RequireAppAttribute.Allows(App.School, RequireAppAttribute.AppOf(customer)));
    }

    [Fact]
    public void A_portal_token_carries_no_staff_permissions() =>
        Assert.Empty(Read(Tokens().CreatePortalToken(Guid.NewGuid(), Guid.NewGuid(), 42, App.School)).FindAll("permission"));
}
