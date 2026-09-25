using Shared.Kernel.Apps;

namespace Master.Api.Services;

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

    /// <summary>
    /// A long-lived secure token for external contacts: a RetailErp customer's
    /// statements, or a School guardian's parent portal (TK-69). The token names
    /// <paramref name="app"/> so the School routes accept it and RetailErp's refuse it.
    /// </summary>
    string CreatePortalToken(Guid customerId, Guid orgId, long contactId, App app = App.RetailErp);
}

public sealed class AccessTokenRequest
{
    public required Guid UserId { get; init; }

    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Minted as customer_code. Set only at signup and never changed, which is
    /// what makes it safe to file documents under: a code that could change
    /// would strand every file stored under the old one.
    /// </summary>
    public required string CustomerCode { get; init; }

    public required Guid OrgId { get; init; }

    /// <summary>The app the token is for; written as the <c>app</c> claim (TK-43).</summary>
    public Shared.Kernel.Apps.App App { get; init; } = Shared.Kernel.Apps.App.RetailErp;

    /// <summary>
    /// The single role this user holds in this organization. A user has exactly
    /// one per branch, which is what makes a per-role period lock resolve to one
    /// date per user with nothing to reconcile.
    /// </summary>
    public required int RoleId { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }

    public required string LicenseStatus { get; init; }

    public DateOnly? LicenseExpiry { get; init; }

    /// <summary>Whether that date is the branch's own rather than the licence's.</summary>
    public bool ExpiryIsBranchLevel { get; init; }

    /// <summary>The trade this branch is in — General, Pharma or Jewellery.</summary>
    public string Vertical { get; init; } = "General";
}
