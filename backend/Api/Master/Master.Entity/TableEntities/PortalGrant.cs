using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// One portal link given to a contact (TK-94).
///
/// The link carries a random code, and only the code's SHA-256 is stored, so a
/// copy of this table opens nobody's portal. The portal exchanges the code for a
/// one-hour token while the grant lives. Revoking the grant stops a fresh token
/// being issued, and a token already issued runs out within the hour: every
/// service validates the token locally, so that hour is the price of revoking.
/// </summary>
public class PortalGrant : OrgScopedEntity
{
    public long PortalGrantId { get; set; }

    public long ContactId { get; set; }

    /// <summary>
    /// The business type of the branch the link was made in (TK-136): a School
    /// branch's link opens the parent portal, any other the statement portal.
    /// Null on a branch with no trade.
    /// </summary>
    public BusinessType? BusinessType { get; set; }

    /// <summary>SHA-256 of the link's code, upper-case hex. The code itself is shown once and never stored.</summary>
    [Required(ErrorMessage = "The code hash is required.")]
    [MaxLength(64, ErrorMessage = "The code hash cannot exceed 64 characters.")]
    public string CodeHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? RevokedBy { get; set; }

    /// <summary>When the code was last exchanged for a session.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }
}
