using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;
using Shared.Kernel.Licensing;

namespace Master.Entity.TableEntities;

/// <summary>
/// A customer asking to move to a higher plan (D-70, TK-209). There is no
/// payment in the app, so the request is the whole upgrade flow on the
/// customer's side: it is recorded here, emailed to the address in
/// <c>Licensing:UpgradeRequestEmail</c>, and a platform operator changes the
/// plan from the admin screens.
/// </summary>
public class UpgradeRequest : AuditableEntity
{
    public long UpgradeRequestId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>The branch the person was in when they asked.</summary>
    public Guid OrgId { get; set; }

    public Guid RequestedByUserId { get; set; }

    /// <summary>The plan the customer was on when it asked.</summary>
    public LicenseType FromPlan { get; set; }

    public LicenseType ToPlan { get; set; }

    /// <summary>The limit that sent them here; empty when they asked from the Plan screen.</summary>
    public PlanLimitKind? Reason { get; set; }

    [MaxLength(500, ErrorMessage = "The note cannot exceed 500 characters.")]
    public string? Note { get; set; }

    public UpgradeRequestStatus Status { get; set; } = UpgradeRequestStatus.Pending;
}
