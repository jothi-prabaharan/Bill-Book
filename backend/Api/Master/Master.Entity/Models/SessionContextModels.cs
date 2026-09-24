namespace Master.Entity.Models;

/// <summary>
/// What a signed-in page needs to know about its session (H0.2, TK-43):
/// who, which branch, which app, whether that app's licence lets them in, and
/// what they may do. <b>No internal ids</b>: the new apps read this instead of
/// decoding the token, and a page has no use for a user, branch or customer id.
/// </summary>
public class SessionContextResponse
{
    public string DisplayName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string BranchName { get; set; } = null!;

    public string BranchCode { get; set; } = null!;

    /// <summary>The app this token is for: RetailErp, School, Hrms or Payroll.</summary>
    public string App { get; set; } = null!;

    /// <summary>That app's licence: Active, Trial, Expired, Suspended or NotLicensed.</summary>
    public string LicenseStatus { get; set; } = null!;

    public DateOnly? LicenseExpiry { get; set; }

    /// <summary>True when the branch's own end date, not the licence's, is the one that governs.</summary>
    public bool ExpiryIsBranchLevel { get; set; }

    /// <summary>The permissions this token carries, as <c>{module}.{action}</c>.</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];

    /// <summary>
    /// The apps this user can switch to in this branch: those where they hold a
    /// role, with each app's licence status. The app switcher draws from this.
    /// </summary>
    public IReadOnlyList<SessionAppResponse> Apps { get; set; } = [];
}

public class SessionAppResponse
{
    public string App { get; set; } = null!;

    public string LicenseStatus { get; set; } = null!;
}
