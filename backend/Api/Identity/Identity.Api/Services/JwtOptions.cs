namespace Identity.Api.Services;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "bill-book";

    public string Audience { get; set; } = "bill-book";

    /// <summary>Symmetric signing key. Must come from config/Key Vault, never hard-coded.</summary>
    public string SigningKey { get; set; } = null!;

    public int PreAuthTokenMinutes { get; set; } = 5;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
