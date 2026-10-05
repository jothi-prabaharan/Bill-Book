using Master.Repository.Clients;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Master.Repository.Repositories;

public sealed class JwtTokenRepository : ITokenRepository
{
    private const string PreAuthMarkerClaim = "pre_auth";

    private readonly JwtOptionsRepository _options;
    private readonly TimeProvider _clock;

    public JwtTokenRepository(IOptions<JwtOptionsRepository> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public string CreatePreAuthToken(Guid userId, string email)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(PreAuthMarkerClaim, "true"),
        };

        return Write(claims, _clock.GetUtcNow().AddMinutes(_options.PreAuthTokenMinutes));
    }

    public string CreateAccessToken(AccessTokenRequest request)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new("customer_id", request.CustomerId.ToString()),
            new("customer_code", request.CustomerCode),
            new("org_id", request.OrgId.ToString()),

            new("display_name", request.DisplayName),
            new("license_status", request.LicenseStatus),

            // The trade this branch is in. Defaults to Retail for every token
            // unless the request says otherwise, so nothing downstream has to
            // handle its absence.
            new("vertical", request.Vertical),

            // The role, beside the permissions it grants. Permissions answer
            // "may this caller do X"; the role answers "which of the branch's
            // period locks applies to them", and that is not derivable from a
            // permission list.
            new("role_id", request.RoleId.ToString()),
        };

        if (request.LicenseExpiry is DateOnly expiry)
        {
            claims.Add(new Claim("license_expiry", expiry.ToString("yyyy-MM-dd")));
        }

        // Only when true. A claim that says "false" on every ordinary token is
        // bytes on every request for the rare case.
        if (request.ExpiryIsBranchLevel)
        {
            claims.Add(new Claim("expiry_scope", "branch"));
        }

        if (request.BusinessType.HasValue)
        {
            claims.Add(new Claim("business_type", request.BusinessType.Value.ToString()));
        }

        claims.Add(new Claim("add_ons", ((int)request.AddOns).ToString()));
        claims.Add(new Claim("plan_tier", request.PlanTier.ToString()));

        foreach (string permission in request.Permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        return Write(claims, _clock.GetUtcNow().AddMinutes(_options.AccessTokenMinutes));
    }

    public Guid? ValidatePreAuthToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        try
        {
            ClaimsPrincipal principal = handler.ValidateToken(token, parameters, out _);
            if (principal.FindFirst(PreAuthMarkerClaim)?.Value != "true")
            {
                return null;
            }

            string? sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(sub, out Guid userId) ? userId : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public (string Token, string Hash, DateTimeOffset ExpiresAt) CreateRefreshToken()
    {
        byte[] raw = RandomNumberGenerator.GetBytes(64);
        string token = Convert.ToBase64String(raw);
        DateTimeOffset expires = _clock.GetUtcNow().AddDays(_options.RefreshTokenDays);
        return (token, HashUtilRepository.Sha256(token), expires);
    }

    /// <summary>How long a portal session lasts. Revoking a grant ends its sessions within this (TK-94).</summary>
    public static readonly TimeSpan PortalSessionLifetime = TimeSpan.FromHours(1);

    public (string Token, DateTimeOffset ExpiresAt) CreatePortalToken(
        Guid customerId, Guid orgId, long contactId, long grantId, BusinessType? businessType = null, string? customerCode = null)
    {
        var claims = new List<Claim>
        {
            new("customer_id", customerId.ToString()),
            new("org_id", orgId.ToString()),
            new(RequirePortalAccessAttribute.ContactClaim, contactId.ToString()),
            new(RequirePortalAccessAttribute.AccessClaim, "true"),
            new(RequirePortalAccessAttribute.GrantClaim, grantId.ToString()),
        };

        if (!string.IsNullOrEmpty(customerCode))
        {
            claims.Add(new Claim("customer_code", customerCode));
        }

        // The branch's business type, as on a staff token (TK-136): a School
        // branch's token opens the parent portal routes, which ask for a School
        // branch, and every service's business-type guard reads it the same way.
        if (businessType is BusinessType type)
        {
            claims.Add(new Claim(ScopeClaims.BusinessTypeClaim, type.ToString()));
        }

        // An hour, not the thirty days a link used to carry: the link is now a
        // code the portal exchanges again, so revoking it has an end (TK-94).
        DateTimeOffset expires = _clock.GetUtcNow().Add(PortalSessionLifetime);
        return (Write(claims, expires), expires);
    }

    private string Write(IEnumerable<Claim> claims, DateTimeOffset expires)
    {
        var credentials = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: _clock.GetUtcNow().UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private SymmetricSecurityKey SigningKey() =>
        new(Encoding.UTF8.GetBytes(_options.SigningKey));
}
