using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Hrm.Entity.TableEntities;

// Organisation setup (H1, TK-48): the four code-and-name masters and the work
// location. Each branch has its own, seeded with one of each. Codes are unique
// per branch; a master is deactivated, never deleted.

/// <summary>A department. May have a head and sit under a parent (no cycles, checked in C#).</summary>
public class Department : OrgScopedEntity
{
    public long DepartmentId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public long? HeadEmployeeId { get; set; }

    public long? ParentDepartmentId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class Designation : OrgScopedEntity
{
    public long DesignationId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

/// <summary>A grade. Leave policies, claim limits and salary structures key off it.</summary>
public class Grade : OrgScopedEntity
{
    public long GradeId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    /// <summary>The notice period a new employee of this grade starts with.</summary>
    [Range(0, 365, ErrorMessage = "Notice period must be between 0 and 365 days.")]
    public int NoticePeriodDays { get; set; } = 30;

    public bool IsActive { get; set; } = true;
}

public class CostCentre : OrgScopedEntity
{
    public long CostCentreId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

/// <summary>Where an employee works. Its state drives professional tax and LWF.</summary>
public class WorkLocation : OrgScopedEntity
{
    public long WorkLocationId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Unenforced — <c>mst.States</c>. Nullable because a branch's seeded
    /// location does not know its state; payroll requires it before computing
    /// professional tax.
    /// </summary>
    public int? StateId { get; set; }

    [MaxLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    public string? AddressLine1 { get; set; }

    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }

    /// <summary>The mobile check-in fence radius. Null means no fence.</summary>
    [Range(1, 100000, ErrorMessage = "The geo-fence must be between 1 and 100000 metres.")]
    public int? GeoFenceMetres { get; set; }

    public bool IsActive { get; set; } = true;
}
