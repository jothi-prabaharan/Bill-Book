namespace Identity.Api.Services;

public interface ITokenService
{
    /// <summary>Short-lived pre-auth token (no org context) issued after step-one login.</summary>
    string CreatePreAuthToken(Guid userId, string email);

    /// <summary>Full access token carrying sub, customer_id, org_id, display_name, permission[] and licence claims.</summary>
    string CreateAccessToken(AccessTokenRequest request);

    /// <summary>Reads and validates a pre-auth token, returning the user id, or null if invalid.</summary>
    Guid? ValidatePreAuthToken(string token);

    /// <summary>A new opaque refresh token plus its SHA-256 hash and expiry.</summary>
    (string Token, string Hash, DateTimeOffset ExpiresAt) CreateRefreshToken();
}

public sealed class AccessTokenRequest
{
    public required Guid UserId { get; init; }

    public required Guid CustomerId { get; init; }

    public required Guid OrgId { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }

    public required string LicenseStatus { get; init; }

    public DateOnly? LicenseExpiry { get; init; }

    /// <summary>Whether that date is the branch's own rather than the licence's.</summary>
    public bool ExpiryIsBranchLevel { get; init; }
}
