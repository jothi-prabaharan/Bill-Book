using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Services;

public sealed class JwtTokenService : ITokenService
{
    private const string PreAuthMarkerClaim = "pre_auth";

    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
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
            new("org_id", request.OrgId.ToString()),
            new("display_name", request.DisplayName),
            new("license_status", request.LicenseStatus),
        };

        if (request.LicenseExpiry is DateOnly expiry)
        {
            claims.Add(new Claim("license_expiry", expiry.ToString("yyyy-MM-dd")));
        }

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

            string? sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
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
        return (token, HashUtil.Sha256(token), expires);
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
