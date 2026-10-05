namespace Master.Entity.Models;

/// <summary>
/// What a signed-in page needs to know about its session (H0.2, TK-43):
/// who, which branch, its business type and add-ons, whether the licence lets
/// them in, and what they may do. <b>No internal ids</b>: the new apps read this instead of
/// decoding the token, and a page has no use for a user, branch or customer id.
/// </summary>
public class SessionContextResponse
{
    public string DisplayName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string BranchName { get; set; } = null!;

    public string BranchCode { get; set; } = null!;

    /// <summary>The business vertical of this branch (D-36, TK-127).</summary>
    public Shared.Kernel.Apps.BusinessType? BusinessType { get; set; }

    /// <summary>Enabled modular add-ons for this branch (D-36, TK-127).</summary>
    public Shared.Kernel.Apps.AddOn AddOns { get; set; } = Shared.Kernel.Apps.AddOn.None;

    /// <summary>The customer's subscription plan tier (TK-129): Trial, Standard/Basic, Pro, Elite.</summary>
    public string PlanTier { get; set; } = "Standard";

    /// <summary>The customer's licence: Active, Trial, Expired or Suspended.</summary>
    public string LicenseStatus { get; set; } = null!;

    public DateOnly? LicenseExpiry { get; set; }

    /// <summary>True when the branch's own end date, not the licence's, is the one that governs.</summary>
    public bool ExpiryIsBranchLevel { get; set; }

    /// <summary>The permissions this token carries, as <c>{module}.{action}</c>.</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];
}
