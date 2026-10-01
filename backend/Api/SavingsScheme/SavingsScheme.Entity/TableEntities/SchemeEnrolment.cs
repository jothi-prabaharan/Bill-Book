using System.ComponentModel.DataAnnotations;
using SavingsScheme.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace SavingsScheme.Entity.TableEntities;

public class SchemeEnrolment : OrgScopedEntity
{
    public long SchemeEnrolmentId { get; set; }

    public long SchemeDefinitionId { get; set; }
    public SchemeDefinition SchemeDefinition { get; set; } = null!;

    public long ContactId { get; set; }

    public DateOnly StartDate { get; set; }

    public decimal InstalmentAmount { get; set; }

    public decimal TotalPrincipalPaid { get; set; }
    
    public decimal BonusAccrued { get; set; }

    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Active;

    public DateOnly? MaturityDate { get; set; }
    
    public DateOnly? LapsedDate { get; set; }

    public DateOnly? RedeemedDate { get; set; }
}
