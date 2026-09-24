using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Hrm.Entity.TableEntities;

/// <summary>
/// A named relation other than the reporting line (TK-49): "Lead", "Project
/// Lead", "Mentor". An approval level can name one ("the employee's Lead"), so
/// a customer adds whatever relations their organisation uses. Seeded with Lead
/// and Project Lead.
/// </summary>
public class RelationshipType : OrgScopedEntity
{
    public long RelationshipTypeId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

/// <summary>Who an employee's Lead (or other relation) is, from a date. One active row per type.</summary>
public class EmployeeRelationship : OrgScopedEntity
{
    public long EmployeeRelationshipId { get; set; }

    public long EmployeeId { get; set; }

    public long RelationshipTypeId { get; set; }

    public long RelatedEmployeeId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly? ToDate { get; set; }
}
