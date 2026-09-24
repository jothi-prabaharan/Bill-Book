using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Customer;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

/// <summary>
/// How fast a branch promises to answer and to resolve a ticket of one priority
/// (D-18, TK-18). One row per priority per branch, seeded when the branch is
/// created; a ticket's <c>SlaDueAt</c> is its creation time plus the
/// resolution hours of its priority's row.
/// </summary>
public class SlaPolicy : OrgScopedEntity
{
    public long SlaPolicyId { get; set; }

    public TicketPriority Priority { get; set; }

    /// <summary>Hours to the first reply.</summary>
    [Range(1, 8760, ErrorMessage = "Response time must be between 1 hour and a year.")]
    public int ResponseHours { get; set; }

    /// <summary>Hours to resolution — what <c>SlaDueAt</c> is set from.</summary>
    [Range(1, 8760, ErrorMessage = "Resolution time must be between 1 hour and a year.")]
    public int ResolutionHours { get; set; }
}
